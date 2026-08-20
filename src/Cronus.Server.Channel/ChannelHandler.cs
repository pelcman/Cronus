using System.Security.Cryptography;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

/// <summary>
/// Handles channel-stage packets for one connection (ports <c>PacketHandler_Game</c> paths for
/// JMS v186). Flow: <c>CP_MigrateIn</c> loads the character, replies <c>LP_SetField</c>, and
/// joins the character's field; afterwards movement (<c>CP_UserMove</c>) and chat
/// (<c>CP_UserChat</c>) relay to everyone else in the same field, and disconnecting announces
/// <c>LP_UserLeaveField</c>.
/// </summary>
public sealed class ChannelHandler : PacketHandlerBase
{
    /// <summary>
    /// Byte length of the JMS v186 CP_UserMove prefix before the CMovePath buffer:
    /// int(-1) ×2, one byte, int(-1) ×2 + int ×2, then one trailing int (JMS >= 164).
    /// </summary>
    private const int MovePrefixLength = 4 + 4 + 1 + (4 * 4) + 4;

    /// <summary>LP_TransferFieldReqIgnored reason: the portal is disabled.</summary>
    private const byte TransferDisabledPortal = 1;

    private readonly ChannelPackets _packets;
    private readonly ICharacterRepository _characters;
    private readonly FieldRegistry _fields;
    private readonly IMapProvider _maps;
    private readonly NpcScriptEngine? _npcScripts;
    private readonly int _channelId;

    private readonly int _opMigrateIn;
    private readonly int _opAliveAck;
    private readonly int _opUserMove;
    private readonly int _opUserChat;
    private readonly int _opMeleeAttack;
    private readonly int _opDropPickUp;
    private readonly int _opTransferField;
    private readonly int _opSelectNpc;
    private readonly int _opScriptAnswer;

    private FieldPlayer? _player;
    private Field? _field;
    private NpcConversation? _conversation;

    public ChannelHandler(
        OpcodeTable clientOpcodes,
        OpcodeTable serverOpcodes,
        ICharacterRepository characters,
        ServerConfig config,
        FieldRegistry? fields = null,
        IMapProvider? maps = null,
        NpcScriptEngine? npcScripts = null,
        int channelId = 0)
    {
        _packets = new ChannelPackets(serverOpcodes, config);
        _characters = characters;
        _fields = fields ?? new FieldRegistry();
        _maps = maps ?? new InMemoryMapProvider(Array.Empty<MapData>());
        _npcScripts = npcScripts;
        _channelId = channelId;

        _opMigrateIn = clientOpcodes.Get(ClientOpcode.MigrateIn);
        _opAliveAck = clientOpcodes.Get(ClientOpcode.AliveAck);
        _opUserMove = clientOpcodes.Get(ClientOpcode.UserMove);
        _opUserChat = clientOpcodes.Get(ClientOpcode.UserChat);
        _opMeleeAttack = clientOpcodes.Get(ClientOpcode.UserMeleeAttack);
        _opDropPickUp = clientOpcodes.Get(ClientOpcode.DropPickUpRequest);
        _opTransferField = clientOpcodes.Get(ClientOpcode.UserTransferFieldRequest);
        _opSelectNpc = clientOpcodes.Get(ClientOpcode.UserSelectNpc);
        _opScriptAnswer = clientOpcodes.Get(ClientOpcode.UserScriptMessageAnswer);
    }

    /// <summary>The character bound to this session after a successful migrate-in.</summary>
    public Character? Player => _player?.Character;

    public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
    {
        if (opcode == _opMigrateIn)
        {
            await HandleMigrateInAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opUserMove)
        {
            await HandleUserMoveAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opUserChat)
        {
            await HandleUserChatAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMeleeAttack)
        {
            await HandleMeleeAttackAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opDropPickUp)
        {
            await HandleDropPickUpAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opTransferField)
        {
            await HandleTransferFieldAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opSelectNpc)
        {
            HandleSelectNpc(session, packet);
        }
        else if (opcode == _opScriptAnswer)
        {
            HandleScriptAnswer(packet);
        }
        else if (opcode == _opAliveAck)
        {
            // Keep-alive acknowledged; nothing to do.
        }
    }

    public override async ValueTask OnDisconnectedAsync(MapleSession session, Exception? error)
    {
        _conversation?.End();
        _conversation = null;

        if (_player is not null && _field is not null)
        {
            _characters.Save(_player.Character); // persist last known map/stats on logout
            _field.Leave(_player.Character.Id);
            await _field.BroadcastAsync(_packets.UserLeaveField(_player.Character.Id)).ConfigureAwait(false);
            _player = null;
            _field = null;
        }
    }

    private async ValueTask HandleMigrateInAsync(MapleSession session, PacketReader packet)
    {
        // JMS v186: [characterId:4][machineId:16][unk:2][unk:1][clientKey:8]
        int characterId = packet.ReadInt();

        Character? character = _characters.Find(characterId);
        if (character is null)
        {
            return;
        }

        var player = new FieldPlayer(character, session);
        _player = player;
        session.UserData = character;

        (int, int, int) seeds = (RandomSeed(), RandomSeed(), RandomSeed());
        await session.SendAsync(_packets.SetFieldEnterGame(character, _channelId, seeds)).ConfigureAwait(false);

        // Join the field: tell the newcomer about everyone already there, and vice versa.
        Field field = _fields.Get(character.MapId);
        foreach (FieldPlayer other in field.Players)
        {
            await session.SendAsync(_packets.UserEnterField(other)).ConfigureAwait(false);
        }

        field.Enter(player);
        _field = field;
        await field.BroadcastAsync(_packets.UserEnterField(player), exceptCharacterId: character.Id)
            .ConfigureAwait(false);

        await SpawnNpcsAsync(session, field).ConfigureAwait(false);
    }

    private async ValueTask SpawnNpcsAsync(MapleSession session, Field field)
    {
        foreach (FieldNpc npc in field.Npcs)
        {
            await session.SendAsync(_packets.NpcEnterField(npc)).ConfigureAwait(false);
        }

        foreach (FieldMob mob in field.Mobs)
        {
            await session.SendAsync(_packets.MobEnterField(mob)).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleUserMoveAsync(PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_UserMove: fixed prefix then the raw CMovePath buffer, which is relayed
        // verbatim (ResCUserRemote.UserMove re-emits the parsed bytes unchanged).
        if (packet.Remaining <= MovePrefixLength)
        {
            return;
        }

        packet.Skip(MovePrefixLength);
        byte[] movePath = packet.ReadRemaining();

        UpdatePositionFromMovePath(_player, movePath);

        await _field.BroadcastAsync(
            _packets.UserMove(_player.Character.Id, movePath),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
    }

    private async ValueTask HandleMeleeAttackAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        AttackInfo attack = AttackParser.ParseMelee(packet);

        foreach (AttackTarget target in attack.Targets)
        {
            FieldMob? mob = _field.FindMob(target.MobObjectId);
            if (mob is null || mob.IsDead)
            {
                continue;
            }

            long damage = target.TotalDamage;
            mob.Damage(damage > int.MaxValue ? int.MaxValue : (int)damage);

            if (mob.IsDead)
            {
                await _field.BroadcastAsync(_packets.MobLeaveField(mob.ObjectId)).ConfigureAwait(false);
                await GrantKillExpAsync(session, mob.Exp).ConfigureAwait(false);
                await DropMesoAsync(mob).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Drops meso from a killed mob (placeholder amount until wz drop tables load).</summary>
    private async ValueTask DropMesoAsync(FieldMob mob)
    {
        if (_field is null)
        {
            return;
        }

        int meso = Math.Max(1, mob.MaxHp / 5); // placeholder formula
        FieldDrop drop = _field.AddMesoDrop(meso, mob.X, mob.Y, mob);
        await _field.BroadcastAsync(_packets.DropEnterFieldMeso(drop)).ConfigureAwait(false);
    }

    private async ValueTask HandleDropPickUpAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_DropPickUpRequest: [unk:1][updateTime:4][x:2][y:2][objectId:4]
        packet.ReadByte();
        packet.ReadInt();
        packet.ReadShort();
        packet.ReadShort();
        int dropOid = packet.ReadInt();

        FieldDrop? drop = _field.RemoveDrop(dropOid);
        if (drop is null)
        {
            return; // already taken
        }

        await _field.BroadcastAsync(_packets.DropLeaveFieldPickup(dropOid, _player.Character.Id))
            .ConfigureAwait(false);

        Character c = _player.Character;
        c.Meso = (int)Math.Clamp((long)c.Meso + drop.Meso, 0, int.MaxValue);
        _characters.Save(c);
        await session.SendAsync(_packets.StatChanged(c, StatFlag.Meso)).ConfigureAwait(false);
    }

    private async ValueTask GrantKillExpAsync(MapleSession session, int exp)
    {
        if (exp <= 0 || _player is null)
        {
            return;
        }

        Character c = _player.Character;
        StatFlag changed = CharacterProgression.GainExp(c, exp); // processes level-ups
        _characters.Save(c);
        await session.SendAsync(_packets.StatChanged(c, changed)).ConfigureAwait(false);
    }

    private async ValueTask HandleUserChatAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186: [timestamp:4][message:str][onlyBalloon:1]
        packet.ReadInt();
        string message = packet.ReadString();
        bool onlyBalloon = packet.Remaining > 0 && packet.ReadBool();

        // Commands (prefix '!') are handled server-side and not broadcast.
        if (message.StartsWith('!'))
        {
            await HandleCommandAsync(session, message[1..]).ConfigureAwait(false);
            return;
        }

        byte[] chat = _packets.UserChat(
            _player.Character.Id, isGm: false, message, onlyBalloon);
        await _field.BroadcastAsync(chat).ConfigureAwait(false);
    }

    /// <summary>
    /// Minimal GM/debug command set for local testing (chat lines starting with '!'). Replies
    /// are echoed back to the caller as their own chat line.
    /// </summary>
    private async ValueTask HandleCommandAsync(MapleSession session, string command)
    {
        string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "map" when parts.Length >= 2 && int.TryParse(parts[1], out int mapId):
                await MovePlayerToMapAsync(session, mapId, spawnPortal: 0).ConfigureAwait(false);
                break;

            case "meso" when parts.Length >= 2 && int.TryParse(parts[1], out int amount):
                _player!.Character.Meso = (int)Math.Clamp((long)_player.Character.Meso + amount, 0, int.MaxValue);
                _characters.Save(_player.Character);
                await session.SendAsync(_packets.StatChanged(_player.Character, StatFlag.Meso)).ConfigureAwait(false);
                break;

            case "notice" when parts.Length >= 2:
                await _field!.BroadcastAsync(_packets.BroadcastNotice(command["notice ".Length..].Trim()))
                    .ConfigureAwait(false);
                break;

            case "pos":
                await ReplyAsync(session, $"pos: ({_player!.X}, {_player.Y}) map {_player.Character.MapId}")
                    .ConfigureAwait(false);
                break;

            case "help":
                await ReplyAsync(session, "commands: !map <id>, !meso <n>, !notice <msg>, !pos, !help")
                    .ConfigureAwait(false);
                break;

            default:
                await ReplyAsync(session, $"unknown command: {parts[0]}").ConfigureAwait(false);
                break;
        }
    }

    /// <summary>Sends a chat line visible only to the calling player (as their own message).</summary>
    private ValueTask ReplyAsync(MapleSession session, string text)
        => session.SendAsync(_packets.UserChat(_player!.Character.Id, isGm: true, text, onlyBalloon: false));

    private void HandleSelectNpc(MapleSession session, PacketReader packet)
    {
        // One conversation at a time; ignore a new NPC while a script is still running.
        if (_player is null || _npcScripts is null || _conversation is { IsEnded: false })
        {
            return;
        }

        // JMS v186 CP_UserSelectNpc: [npcObjectId:4][x:2][y:2]. The client sends the runtime
        // object id; resolve it to the template id (the script key) via the field.
        int objectId = packet.ReadInt();
        int templateId = _field?.FindNpc(objectId)?.TemplateId ?? objectId;

        var dialog = new ChannelNpcDialog(session, _packets);
        var player = new ChannelPlayer(_player.Character, _characters, session, _packets);
        _conversation = _npcScripts.Start(templateId, dialog, player);
    }

    private void HandleScriptAnswer(PacketReader packet)
    {
        NpcConversation? conversation = _conversation;
        if (conversation is null || conversation.IsEnded)
        {
            _conversation = null;
            return;
        }

        // JMS v186 CP_UserScriptMessageAnswer: [nMsgType:1][action:1][payload by type]
        int messageType = packet.ReadByte();
        int action = (sbyte)packet.ReadByte();
        int selection = -1;
        string text = string.Empty;

        if (action != 0)
        {
            switch (messageType)
            {
                case 5:  // SM_ASKMENU
                    selection = packet.ReadInt();
                    break;
                case 3:  // SM_ASKTEXT
                    text = packet.ReadString();
                    break;
                case 8:  // SM_ASKAVATAR
                    selection = packet.ReadByte();
                    break;
                case 15: // SM_ASKSLIDEMENU
                    selection = packet.ReadInt();
                    break;
            }
        }

        conversation.Advance(messageType, action, selection, text);
    }

    private async ValueTask HandleTransferFieldAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_UserTransferFieldRequest:
        //   [portalCount:1][mapId:4][portalName:str][x:2,y:2 if portal][unk:1][reviveType:1]
        packet.ReadByte();
        int targetMapId = packet.ReadInt();
        string portalName = packet.ReadString();

        // A direct map id (portal name empty) is a /map-style jump: honor it as-is.
        if (string.IsNullOrEmpty(portalName))
        {
            if (targetMapId < 0)
            {
                await session.SendAsync(_packets.TransferFieldReqIgnored(TransferDisabledPortal)).ConfigureAwait(false);
                return;
            }

            await MovePlayerToMapAsync(session, targetMapId, spawnPortal: 0).ConfigureAwait(false);
            return;
        }

        // Portal-by-name: look up the portal on the current map and follow its link.
        MapData? currentMap = _maps.GetMap(_player.Character.MapId);
        PortalData? portal = currentMap?.FindPortal(portalName);
        if (portal is null || !portal.LinksToMap)
        {
            await session.SendAsync(_packets.TransferFieldReqIgnored(TransferDisabledPortal)).ConfigureAwait(false);
            return;
        }

        int spawn = ResolveSpawnPortal(portal.TargetMapId, portal.TargetName);
        await MovePlayerToMapAsync(session, portal.TargetMapId, spawn).ConfigureAwait(false);
    }

    /// <summary>Finds the spawn portal id in the destination map by its target-portal name.</summary>
    private int ResolveSpawnPortal(int targetMapId, string targetPortalName)
    {
        MapData? target = _maps.GetMap(targetMapId);
        PortalData? spawn = string.IsNullOrEmpty(targetPortalName)
            ? target?.SpawnPortal
            : target?.FindPortal(targetPortalName) ?? target?.SpawnPortal;
        return spawn?.Id ?? 0;
    }

    /// <summary>
    /// Moves the bound player to another map: leave + announce, switch fields, SetField
    /// (map-change branch), then exchange enter-field packets in the new map.
    /// </summary>
    private async ValueTask MovePlayerToMapAsync(MapleSession session, int targetMapId, int spawnPortal)
    {
        FieldPlayer player = _player!;
        Field oldField = _field!;

        oldField.Leave(player.Character.Id);
        await oldField.BroadcastAsync(_packets.UserLeaveField(player.Character.Id)).ConfigureAwait(false);

        player.Character.MapId = targetMapId;
        player.Character.Portal = (byte)spawnPortal;
        _characters.Save(player.Character); // DB-backed repos need an explicit flush

        await session.SendAsync(_packets.SetFieldChangeMap(player.Character, _channelId)).ConfigureAwait(false);

        Field newField = _fields.Get(targetMapId);
        foreach (FieldPlayer other in newField.Players)
        {
            await session.SendAsync(_packets.UserEnterField(other)).ConfigureAwait(false);
        }

        newField.Enter(player);
        _field = newField;
        await newField.BroadcastAsync(_packets.UserEnterField(player), exceptCharacterId: player.Character.Id)
            .ConfigureAwait(false);

        await SpawnNpcsAsync(session, newField).ConfigureAwait(false);
    }

    /// <summary>
    /// Extracts the start position from a CMovePath buffer:
    /// <c>[startX:2][startY:2]...</c> (CMovePath::Decode reads the head as the origin point).
    /// </summary>
    private static void UpdatePositionFromMovePath(FieldPlayer player, byte[] movePath)
    {
        if (movePath.Length < 4)
        {
            return;
        }

        player.X = (short)(movePath[0] | (movePath[1] << 8));
        player.Y = (short)(movePath[2] | (movePath[3] << 8));
    }

    private static int RandomSeed() => RandomNumberGenerator.GetInt32(int.MaxValue);
}
