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
    private readonly ISkillProvider _skills;
    private readonly NpcScriptEngine? _npcScripts;
    private readonly int _channelId;

    private readonly int _opMigrateIn;
    private readonly int _opAliveAck;
    private readonly int _opUserMove;
    private readonly int _opUserChat;
    private readonly int _opUserEmotion;
    private readonly int _opUserSit;
    private readonly int _opMeleeAttack;
    private readonly int _opMagicAttack;
    private readonly int _opShootAttack;
    private readonly int _opUserHit;
    private readonly int _opDropPickUp;
    private readonly int _opSkillUp;
    private readonly int _opMobMove;
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
        ISkillProvider? skills = null,
        int channelId = 0)
    {
        _packets = new ChannelPackets(serverOpcodes, config);
        _characters = characters;
        _fields = fields ?? new FieldRegistry();
        _maps = maps ?? new InMemoryMapProvider(Array.Empty<MapData>());
        _skills = skills ?? NullSkillProvider.Instance;
        _npcScripts = npcScripts;
        _channelId = channelId;

        _opMigrateIn = clientOpcodes.Get(ClientOpcode.MigrateIn);
        _opAliveAck = clientOpcodes.Get(ClientOpcode.AliveAck);
        _opUserMove = clientOpcodes.Get(ClientOpcode.UserMove);
        _opUserChat = clientOpcodes.Get(ClientOpcode.UserChat);
        _opUserEmotion = clientOpcodes.Get(ClientOpcode.UserEmotion);
        _opUserSit = clientOpcodes.Get(ClientOpcode.UserSitRequest);
        _opMeleeAttack = clientOpcodes.Get(ClientOpcode.UserMeleeAttack);
        _opMagicAttack = clientOpcodes.Get(ClientOpcode.UserMagicAttack);
        _opShootAttack = clientOpcodes.Get(ClientOpcode.UserShootAttack);
        _opUserHit = clientOpcodes.Get(ClientOpcode.UserHit);
        _opDropPickUp = clientOpcodes.Get(ClientOpcode.DropPickUpRequest);
        _opSkillUp = clientOpcodes.Get(ClientOpcode.UserSkillUpRequest);
        _opMobMove = clientOpcodes.Get(ClientOpcode.MobMove);
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
        else if (opcode == _opUserEmotion)
        {
            await HandleUserEmotionAsync(packet).ConfigureAwait(false);
        }
        else if (opcode == _opUserSit)
        {
            await HandleUserSitAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMeleeAttack)
        {
            await HandleMeleeAttackAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMagicAttack)
        {
            await HandleMagicAttackAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opShootAttack)
        {
            await HandleShootAttackAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opUserHit)
        {
            await HandleUserHitAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opDropPickUp)
        {
            await HandleDropPickUpAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opSkillUp)
        {
            await HandleSkillUpAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMobMove)
        {
            await HandleMobMoveAsync(session, packet).ConfigureAwait(false);
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
            await ReleaseControlledMobsAsync(_field, _player.Character.Id).ConfigureAwait(false);
            _player = null;
            _field = null;
        }
    }

    /// <summary>
    /// Releases mobs controlled by a departing player, handing them to another player in the
    /// field when one is present (they receive LP_MobChangeController).
    /// </summary>
    private async ValueTask ReleaseControlledMobsAsync(Field field, int departingCharacterId)
    {
        FieldPlayer? successor = field.Players.FirstOrDefault();

        foreach (FieldMob mob in field.Mobs)
        {
            if (mob.ControllerId != departingCharacterId)
            {
                continue;
            }

            if (successor is null || mob.IsDead)
            {
                mob.ControllerId = -1;
                continue;
            }

            mob.ControllerId = successor.Character.Id;
            try
            {
                await successor.Session.SendAsync(_packets.MobChangeController(mob)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                mob.ControllerId = -1; // successor is going away too
            }
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

        // Post-SetField initialization the JMS v186 client expects to finish entering the field.
        // Byte-for-byte order captured from the reference server's ResCClientSocket.OnMigrateIn:
        // StatChanged -> ForcedStatReset -> pet-consume ×3 -> key map -> macros -> buddy list ->
        // family info -> broadcast slide. (LP_FamilyPrivilegeList, a large version-constant table,
        // is intentionally not sent yet - see ChannelPackets.FamilyInfoResult.)
        await session.SendAsync(_packets.StatChanged(character, (StatFlag)0)).ConfigureAwait(false);
        await session.SendAsync(_packets.ForcedStatReset()).ConfigureAwait(false);
        await session.SendAsync(_packets.PetConsumeItemInit()).ConfigureAwait(false);
        await session.SendAsync(_packets.PetConsumeMpItemInit()).ConfigureAwait(false);
        await session.SendAsync(_packets.PetConsumeCureItemInit()).ConfigureAwait(false);
        await session.SendAsync(_packets.FuncKeyMappedInit()).ConfigureAwait(false);
        await session.SendAsync(_packets.MacroSysDataInit()).ConfigureAwait(false);
        await session.SendAsync(_packets.FriendListInit()).ConfigureAwait(false);
        await session.SendAsync(_packets.FamilyInfoResult()).ConfigureAwait(false);
        await session.SendAsync(_packets.BroadcastSlideClear()).ConfigureAwait(false);

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

        int characterId = _player!.Character.Id;
        foreach (FieldMob mob in field.Mobs)
        {
            if (mob.IsDead)
            {
                continue;
            }

            await session.SendAsync(_packets.MobEnterField(mob)).ConfigureAwait(false);

            // Uncontrolled mobs are delegated to this client (client-side AI simulation).
            if (mob.ControllerId is -1 || mob.ControllerId == characterId)
            {
                mob.ControllerId = characterId;
                await session.SendAsync(_packets.MobChangeController(mob)).ConfigureAwait(false);
            }
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
        _player.LastActiveTick = Environment.TickCount64; // moving delays HP/MP regen
        _player.Seated = false;                            // and stands you up

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
        await _field.BroadcastAsync(
            _packets.UserMeleeAttack(_player.Character.Id, _player.Character.Level, attack),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
        await ApplyAttackDamageAsync(session, attack).ConfigureAwait(false);
    }

    private async ValueTask HandleMagicAttackAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        AttackInfo attack = AttackParser.ParseMagic(packet); // v186: same layout as melee
        await _field.BroadcastAsync(
            _packets.UserMagicAttack(_player.Character.Id, _player.Character.Level, attack),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
        await ApplyAttackDamageAsync(session, attack).ConfigureAwait(false);
    }

    private async ValueTask HandleShootAttackAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        AttackInfo attack = AttackParser.ParseShoot(packet);

        // The bullet item id isn't resolved yet (no USE-inventory model) — send 0; the shot still
        // fires and applies damage, it just may not render a specific arrow. Follow-up: resolve
        // the bullet from the shooter's inventory slot and consume it.
        await _field.BroadcastAsync(
            _packets.UserShootAttack(_player.Character.Id, _player.Character.Level, attack, bulletItemId: 0, _player.X, _player.Y),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
        await ApplyAttackDamageAsync(session, attack).ConfigureAwait(false);
    }

    /// <summary>
    /// Applies an attack's per-target damage to the mobs in the field: hurts each live target,
    /// and on death releases control, announces the leave, grants exp, and drops meso. Shared by
    /// the melee / magic / ranged handlers. Damage is currently client-reported (see AGENTS.md).
    /// </summary>
    private async ValueTask ApplyAttackDamageAsync(MapleSession session, AttackInfo attack)
    {
        if (_player is not null)
        {
            _player.LastActiveTick = Environment.TickCount64; // attacking delays HP/MP regen
            _player.Seated = false;                            // and stands you up
        }

        foreach (AttackTarget target in attack.Targets)
        {
            FieldMob? mob = _field!.FindMob(target.MobObjectId);
            if (mob is null || mob.IsDead)
            {
                continue;
            }

            // Server authority: bound the client-reported damage to what a legit pre-BB client
            // can produce (per-line cap) rather than trusting target.TotalDamage verbatim.
            long damage = DamageValidator.ValidatedDamage(target);
            mob.Damage(damage > int.MaxValue ? int.MaxValue : (int)damage);

            if (mob.IsDead)
            {
                mob.ControllerId = -1;
                mob.RespawnAtTick = MobRespawnService.NextRespawnTick(mob.MobTime); // 0 = never (boss)
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

    /// <summary>
    /// Byte length of the JMS v186 CP_MobMove fields between the skill int and the CMovePath:
    /// int ×2 (JMS &gt;= 186), byte, int, 0xFFDDCC ×2, int.
    /// </summary>
    private const int MobMoveMidLength = 4 + 4 + 1 + 4 + 4 + 4 + 4;

    private async ValueTask HandleMobMoveAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_MobMove:
        //   [mobOid:4][moveId:2][nextAttack:1][left:1][mobSkill:4][mid fields][movePath raw]
        int mobOid = packet.ReadInt();
        short moveId = packet.ReadShort();
        bool nextAttackPossible = packet.ReadBool();
        byte left = packet.ReadByte();
        int mobSkill = packet.ReadInt();

        if (packet.Remaining <= MobMoveMidLength)
        {
            return;
        }

        packet.Skip(MobMoveMidLength);
        byte[] movePath = packet.ReadRemaining();

        FieldMob? mob = _field.FindMob(mobOid);
        if (mob is null || mob.IsDead)
        {
            return;
        }

        // Only the assigned controller may steer the mob; adopt it if it has none.
        int characterId = _player.Character.Id;
        if (mob.ControllerId is -1)
        {
            mob.ControllerId = characterId;
        }
        else if (mob.ControllerId != characterId)
        {
            return;
        }

        // Track the path origin as the mob's position (same convention as player movement).
        if (movePath.Length >= 4)
        {
            mob.X = (short)(movePath[0] | (movePath[1] << 8));
            mob.Y = (short)(movePath[2] | (movePath[3] << 8));
        }

        await session.SendAsync(_packets.MobCtrlAck(mob, moveId, aggro: false)).ConfigureAwait(false);
        await _field.BroadcastAsync(
            _packets.MobMove(mob.ObjectId, nextAttackPossible, left, mobSkill, movePath),
            exceptCharacterId: characterId).ConfigureAwait(false);
    }

    /// <summary>
    /// Handles <c>CP_UserHit</c> — the client reports the damage its player took from a mob.
    /// Applies the HP loss and pushes <c>LP_StatChanged</c>. HP is floored at 1 for now (death /
    /// revive is a follow-up). Damage is client-reported, the MapleStory norm (see AGENTS.md).
    /// </summary>
    private async ValueTask HandleUserHitAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        // JMS v186 CP_UserHit prefix: [time:4][nAttackIdx:1][nMagicElemAttr:1][nDamage:4] ...
        packet.ReadInt();   // time
        packet.ReadByte();  // nAttackIdx
        packet.ReadByte();  // nMagicElemAttr
        int damage = DamageValidator.ClampLine(packet.ReadInt()); // bound the client-reported hit
        if (damage <= 0)
        {
            return; // a miss / no damage
        }

        Character c = _player.Character;
        _player.LastActiveTick = Environment.TickCount64; // taking a hit counts as activity
        c.Hp = (short)Math.Max(0, c.Hp - damage);          // 0 HP = dead (client shows the tombstone)

        StatFlag changed = StatFlag.Hp;
        if (c.Hp == 0)
        {
            changed |= CharacterProgression.ApplyDeathPenalty(c); // dying costs some exp
        }

        await session.SendAsync(_packets.StatChanged(c, changed)).ConfigureAwait(false);
    }

    private async ValueTask HandleSkillUpAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        // JMS v186 CP_UserSkillUpRequest: [timeStamp:4][skillId:4]
        packet.ReadInt();
        int skillId = packet.ReadInt();

        Character c = _player.Character;
        if (c.Sp <= 0)
        {
            return; // no SP to spend
        }

        c.Skills.TryGetValue(skillId, out int current);

        // Cap at the skill's wz max level (when known) so SP can't over-level a skill.
        int maxLevel = _skills.GetMaxLevel(skillId);
        if (maxLevel > 0 && current >= maxLevel)
        {
            return; // already maxed
        }

        int level = current + 1;
        c.Skills[skillId] = level;
        c.Sp = (short)Math.Max(0, c.Sp - 1);
        _characters.Save(c);

        await session.SendAsync(_packets.StatChanged(c, StatFlag.Sp)).ConfigureAwait(false);
        await session.SendAsync(_packets.ChangeSkillRecordResult(skillId, level)).ConfigureAwait(false);
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

    /// <summary>
    /// Handles <c>CP_UserSitRequest</c> — seats the player on a chair (or stands them when the
    /// seat id is -1) and echoes <c>LP_UserSitResult</c>. Sitting makes HP/MP regen fast and
    /// immediate (see <c>PlayerRegenService</c>).
    /// </summary>
    private async ValueTask HandleUserSitAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        short seatId = packet.ReadShort(); // JMS v186 CP_UserSitRequest: [seatId:2] (-1 = stand)
        _player.Seated = seatId != -1;
        await session.SendAsync(_packets.UserSitResult(seatId)).ConfigureAwait(false);
    }

    private async ValueTask HandleUserEmotionAsync(PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // JMS v186 CP_UserEmotion: [emotionId:4]. Basic emotes are 1..7; item-based face
        // expressions (>7) need the item (not modelled here), but relaying one is harmless.
        int emotion = packet.ReadInt();
        if (emotion <= 0)
        {
            return;
        }

        await _field.BroadcastAsync(
            _packets.UserEmotion(_player.Character.Id, emotion),
            exceptCharacterId: _player.Character.Id).ConfigureAwait(false);
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

        // A dead player dismissing the tombstone dialog sends a transfer request; revive them at
        // this map's return town (or in place) with full HP/MP instead of a normal transfer.
        if (_player.Character.Hp <= 0)
        {
            await ReviveAsync(session).ConfigureAwait(false);
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
    /// Revives a dead player: restores full HP/MP, then transfers to this map's return town (or
    /// the same map when it has none), which clears the client's death state.
    /// </summary>
    private async ValueTask ReviveAsync(MapleSession session)
    {
        Character c = _player!.Character;
        c.Hp = c.MaxHp;
        c.Mp = c.MaxMp;

        int reviveMap = _maps.GetMap(c.MapId)?.ReviveMap ?? c.MapId;
        await MovePlayerToMapAsync(session, reviveMap, spawnPortal: 0).ConfigureAwait(false);
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
        await ReleaseControlledMobsAsync(oldField, player.Character.Id).ConfigureAwait(false);

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
