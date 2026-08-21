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
    private readonly MessengerRegistry _messengers;
    private readonly PartyRegistry _parties;
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
    private readonly int _opWhisper;
    private readonly int _opMessenger;
    private readonly int _opPartyRequest;

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
        int channelId = 0,
        MessengerRegistry? messengers = null,
        PartyRegistry? parties = null)
    {
        _packets = new ChannelPackets(serverOpcodes, config);
        _characters = characters;
        _fields = fields ?? new FieldRegistry();
        _maps = maps ?? new InMemoryMapProvider(Array.Empty<MapData>());
        _skills = skills ?? NullSkillProvider.Instance;
        _npcScripts = npcScripts;
        _channelId = channelId;
        _messengers = messengers ?? new MessengerRegistry(_packets);
        _parties = parties ?? new PartyRegistry();

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
        _opWhisper = clientOpcodes.Get(ClientOpcode.Whisper);
        _opMessenger = clientOpcodes.Get(ClientOpcode.Messenger);
        _opPartyRequest = clientOpcodes.Get(ClientOpcode.PartyRequest);
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
        else if (opcode == _opWhisper)
        {
            await HandleWhisperAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opMessenger)
        {
            await HandleMessengerAsync(session, packet).ConfigureAwait(false);
        }
        else if (opcode == _opPartyRequest)
        {
            await HandlePartyRequestAsync(session, packet).ConfigureAwait(false);
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

            // Leave any messenger so the other members' windows drop this player.
            Messenger? messenger = _messengers.GetFor(_player.Character.Id);
            if (messenger is not null)
            {
                await messenger.LeaveAsync(_player.Character.Id).ConfigureAwait(false);
                _messengers.Unregister(_player.Character.Id, messenger);
            }

            // Leave any party (the leader dropping disbands it — a documented simplification).
            Party? party = _parties.GetForCharacter(_player.Character.Id);
            if (party is not null)
            {
                await LeavePartyAsync(party, _player, byDisconnect: true).ConfigureAwait(false);
            }

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

        // Show the newcomer the meso drops already lying on the ground (no fall animation).
        foreach (FieldDrop drop in field.Drops)
        {
            await session.SendAsync(_packets.DropEnterFieldMeso(drop, onGround: true)).ConfigureAwait(false);
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
                await GrantKillExpAsync(mob.Exp).ConfigureAwait(false);
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
        await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false); // party sees the health drop
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

    private async ValueTask GrantKillExpAsync(int exp)
    {
        if (exp <= 0 || _player is null)
        {
            return;
        }

        Party? party = _parties.GetForCharacter(_player.Character.Id);
        if (party is null)
        {
            await GrantExpToAsync(_player, exp).ConfigureAwait(false); // solo: full exp
            return;
        }

        // Split among party members on the same map; the killer gets the largest share.
        int killerId = _player.Character.Id;
        int killerMap = _player.Character.MapId;
        List<FieldPlayer> sameMap = party.Members.Where(m => m.Character.MapId == killerMap).ToList();

        foreach (FieldPlayer member in sameMap)
        {
            int share = CharacterProgression.PartyExpShare(exp, sameMap.Count, isKiller: member.Character.Id == killerId);
            if (share > 0)
            {
                await GrantExpToAsync(member, share).ConfigureAwait(false);
            }
        }
    }

    /// <summary>Adds exp to one player (processing level-ups) and pushes the stat + level-up effect.</summary>
    private async ValueTask GrantExpToAsync(FieldPlayer recipient, int exp)
    {
        Character c = recipient.Character;
        StatFlag changed = CharacterProgression.GainExp(c, exp); // processes level-ups
        _characters.Save(c);
        await TrySendAsync(recipient, _packets.StatChanged(c, changed)).ConfigureAwait(false);

        // A level-up plays a show effect: the local client triggers its own from the stat change,
        // so only the remote animation (for onlookers in the field) needs broadcasting.
        if (changed.HasFlag(StatFlag.Level) && _field is not null)
        {
            await _field.BroadcastAsync(
                _packets.UserEffectRemote(c.Id, ChannelPackets.UserEffectLevelUp),
                exceptCharacterId: c.Id).ConfigureAwait(false);
            await RefreshPartyWindowAsync(recipient).ConfigureAwait(false); // party window shows the new level
        }
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

    // CP_Whisper operation bits (ports Ops_Whisper): the client ORs WP_Request(0x04) onto the
    // location/whisper op; strip it to recover which one was asked for.
    private const int WpRequest = 0x04;
    private const int WpLocationOp = 0x01;
    private const int WpWhisperOp = 0x02;

    /// <summary>
    /// Handles <c>CP_Whisper</c> — both a private message (WP_Whisper) and a "/find" location
    /// lookup (WP_Location). Finds the target on this channel by name and routes the message /
    /// answers the lookup (ports <c>ReqCUser.OnWhisper</c>).
    /// </summary>
    private async ValueTask HandleWhisperAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        int operation = packet.ReadByte();
        int op = operation & ~WpRequest; // drop the WP_Request bit

        if (op == WpLocationOp)
        {
            string targetName = packet.ReadString();
            FieldPlayer? target = _fields.FindPlayerByName(targetName);
            await session.SendAsync(
                _packets.WhisperLocationResult(targetName, target?.Character.MapId ?? 0, target is not null))
                .ConfigureAwait(false);
            return;
        }

        if (op == WpWhisperOp)
        {
            string targetName = packet.ReadString();
            string message = packet.ReadString();
            FieldPlayer? target = _fields.FindPlayerByName(targetName);

            // Ack the sender: was it delivered?
            await session.SendAsync(_packets.WhisperResult(targetName, target is not null))
                .ConfigureAwait(false);

            // Deliver to the recipient (skip when they whisper themselves — the ack is enough).
            if (target is not null && target.Character.Id != _player.Character.Id)
            {
                await target.Session.SendAsync(
                    _packets.WhisperReceive(_player.Character.Name, _channelId, message))
                    .ConfigureAwait(false);
            }
        }
    }

    // CP_Messenger sub-operations the client sends (ports OpsMessenger).
    private const int MsmpEnterOp = 0;
    private const int MsmpLeaveOp = 2;
    private const int MsmpInviteOp = 3;
    private const int MsmpChatOp = 6;

    /// <summary>
    /// Handles <c>CP_Messenger</c> — the 3-person messenger window: create/join (Enter), leave,
    /// invite a player, and chat (ports <c>ReqCUIMessenger.OnMessenger</c> + <c>TacosMessenger</c>).
    /// Block-list and avatar-refresh ops are out of scope (no block/appearance systems yet).
    /// </summary>
    private async ValueTask HandleMessengerAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        int op = packet.ReadByte();
        int myId = _player.Character.Id;
        Messenger? current = _messengers.GetFor(myId);

        switch (op)
        {
            case MsmpEnterOp:
            {
                if (current is not null)
                {
                    return; // already in a messenger
                }

                int messengerId = packet.ReadInt();
                Messenger? target = messengerId == 0 ? _messengers.Create() : _messengers.FindById(messengerId);
                if (target is null)
                {
                    return; // invite expired / bad id
                }

                if (await target.EnterAsync(_player, _channelId).ConfigureAwait(false))
                {
                    _messengers.Register(myId, target);
                }

                return;
            }

            case MsmpLeaveOp:
            {
                if (current is null)
                {
                    return;
                }

                await current.LeaveAsync(myId).ConfigureAwait(false);
                _messengers.Unregister(myId, current);
                return;
            }

            case MsmpInviteOp:
            {
                if (current is null)
                {
                    return;
                }

                string inviteeName = packet.ReadString();
                FieldPlayer? invitee = _fields.FindPlayerByName(inviteeName);
                // Available only if online and not already in a messenger.
                bool available = invitee is not null && _messengers.GetFor(invitee.Character.Id) is null;

                await current.BroadcastInviteResultAsync(inviteeName, available).ConfigureAwait(false);
                if (available)
                {
                    await invitee!.Session.SendAsync(
                        _packets.MessengerInvite(_player.Character.Name, _channelId, current.Id)).ConfigureAwait(false);
                }

                return;
            }

            case MsmpChatOp:
            {
                if (current is null)
                {
                    return;
                }

                string message = packet.ReadString();
                await current.ChatAsync(myId, message).ConfigureAwait(false);
                return;
            }
        }
    }

    // CP_PartyRequest sub-operations the client sends (ports OpsParty).
    private const int PartyOpCreate = 1;
    private const int PartyOpWithdraw = 2;
    private const int PartyOpJoin = 3;
    private const int PartyOpInvite = 4;
    private const int PartyOpKick = 5;
    private const int PartyOpChangeLeader = 6;

    /// <summary>The 1-based party channel (the reference numbers channels from 1; Cronus from 0).</summary>
    private int PartyChannel => _channelId + 1;

    /// <summary>
    /// Handles <c>CP_PartyRequest</c> — create, invite, join, leave/disband, expel, and change
    /// leader (ports <c>ReqCUser.OnPartyRequest</c> + <c>OdinWorld.Party.updateParty</c>). Parties
    /// are in-memory and online-only; exp sharing and party HP bars are follow-ups.
    /// </summary>
    private async ValueTask HandlePartyRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        int type = packet.ReadByte();
        int myId = _player.Character.Id;
        Party? party = _parties.GetForCharacter(myId);

        switch (type)
        {
            case PartyOpCreate:
            {
                if (party is not null)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrAlreadyJoined)).ConfigureAwait(false);
                    return;
                }

                Party created = _parties.Create(_player);
                await session.SendAsync(_packets.PartyCreateDone(created.Id)).ConfigureAwait(false);
                return;
            }

            case PartyOpWithdraw:
            {
                if (party is null)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrWithdrawUnknown)).ConfigureAwait(false);
                    return;
                }

                await LeavePartyAsync(party, _player, byDisconnect: false).ConfigureAwait(false);
                return;
            }

            case PartyOpJoin:
            {
                int partyId = packet.ReadInt();
                if (party is not null)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrAlreadyInParty)).ConfigureAwait(false);
                    return;
                }

                Party? target = _parties.GetById(partyId);
                if (target is null)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrJoinUnknown)).ConfigureAwait(false);
                    return;
                }

                if (!target.TryAdd(_player))
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrFull)).ConfigureAwait(false);
                    return;
                }

                _parties.Register(myId, target);
                byte[] joinPacket = _packets.PartyJoin(target.Id, _player.Character.Name, target.ViewSlots(PartyChannel), target.LeaderId, PartyChannel);
                await PartyBroadcastAsync(target, joinPacket).ConfigureAwait(false);
                await SyncPartyHpAsync(target, _player).ConfigureAwait(false);
                return;
            }

            case PartyOpInvite:
            {
                string inviteeName = packet.ReadString();
                if (party is null || party.IsFull)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrFull)).ConfigureAwait(false);
                    return;
                }

                FieldPlayer? invitee = _fields.FindPlayerByName(inviteeName);
                if (invitee is null)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrUnknownUser)).ConfigureAwait(false);
                    return;
                }

                if (_parties.GetForCharacter(invitee.Character.Id) is not null)
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrAlreadyInParty)).ConfigureAwait(false);
                    return;
                }

                await session.SendAsync(_packets.PartyInviteSent(inviteeName)).ConfigureAwait(false);
                await invitee.Session.SendAsync(
                    _packets.PartyInvite(party.Id, _player.Character.Name, _player.Character.Level, _player.Character.Job)).ConfigureAwait(false);
                return;
            }

            case PartyOpKick:
            {
                int kickId = packet.ReadInt();
                if (party is null || !party.IsLeader(myId))
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrKickUnknown)).ConfigureAwait(false);
                    return;
                }

                FieldPlayer? kicked = party.MemberById(kickId);
                if (kicked is null || kickId == myId)
                {
                    return; // can't kick a non-member or yourself
                }

                party.Remove(kickId);
                _parties.Unregister(kickId);
                byte[] expel = _packets.PartyDepart(party.Id, kickId, kicked.Character.Name, PartyDepart.Expel, party.ViewSlots(PartyChannel), party.LeaderId, PartyChannel);
                await PartyBroadcastAsync(party, expel).ConfigureAwait(false);
                await TrySendAsync(kicked, expel).ConfigureAwait(false);
                return;
            }

            case PartyOpChangeLeader:
            {
                int newLeaderId = packet.ReadInt();
                if (party is null || !party.IsLeader(myId) || !party.Contains(newLeaderId))
                {
                    await session.SendAsync(_packets.PartyResultSimple(ChannelPackets.PartyErrChangeBossUnknown)).ConfigureAwait(false);
                    return;
                }

                party.SetLeader(newLeaderId);
                byte[] change = _packets.PartyChangeLeader(newLeaderId, byDisconnect: false);
                await PartyBroadcastAsync(party, change).ConfigureAwait(false);
                return;
            }
        }
    }

    /// <summary>
    /// Removes a member from their party: the leader leaving disbands it (everyone is notified),
    /// a member leaving notifies the rest and the leaver. Shared by the withdraw op and disconnect.
    /// </summary>
    private async ValueTask LeavePartyAsync(Party party, FieldPlayer leaver, bool byDisconnect)
    {
        int leaverId = leaver.Character.Id;
        string leaverName = leaver.Character.Name;

        if (party.IsLeader(leaverId))
        {
            // Disband: notify all members while they're still listed, then drop the party.
            byte[] disband = _packets.PartyDepart(party.Id, leaverId, leaverName, PartyDepart.Disband, party.ViewSlots(PartyChannel), party.LeaderId, PartyChannel);
            await PartyBroadcastAsync(party, disband).ConfigureAwait(false);
            _parties.Disband(party);
            return;
        }

        party.Remove(leaverId);
        _parties.Unregister(leaverId);
        byte[] leave = _packets.PartyDepart(party.Id, leaverId, leaverName, PartyDepart.Leave, party.ViewSlots(PartyChannel), party.LeaderId, PartyChannel);
        await PartyBroadcastAsync(party, leave).ConfigureAwait(false); // remaining members
        if (!byDisconnect)
        {
            await TrySendAsync(leaver, leave).ConfigureAwait(false);   // and the leaver's own window
        }
    }

    private static async ValueTask PartyBroadcastAsync(Party party, byte[] packet)
    {
        foreach (FieldPlayer member in party.Members)
        {
            await TrySendAsync(member, packet).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Pushes a player's HP bar (<c>LP_UserHP</c>) to their same-map party members so partners and
    /// healers see it change. No-op outside a party (ports <c>MapleCharacter.updatePartyMemberHP</c>).
    /// </summary>
    private async ValueTask NotifyPartyOfMyHpAsync(FieldPlayer who)
    {
        Party? party = _parties.GetForCharacter(who.Character.Id);
        if (party is null)
        {
            return;
        }

        Character me = who.Character;
        byte[] hp = _packets.UserHP(me.Id, me.Hp, me.MaxHp);
        foreach (FieldPlayer member in party.Members)
        {
            if (member.Character.Id != me.Id && member.Character.MapId == me.MapId)
            {
                await TrySendAsync(member, hp).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Rebroadcasts the party window to all members so a member's changed map or level shows up
    /// (the silent-update op; ports the <c>SILENT_UPDATE</c> path). No-op outside a party.
    /// </summary>
    private async ValueTask RefreshPartyWindowAsync(FieldPlayer member)
    {
        Party? party = _parties.GetForCharacter(member.Character.Id);
        if (party is null)
        {
            return;
        }

        byte[] refresh = _packets.PartyRefresh(party.Id, party.ViewSlots(PartyChannel), party.LeaderId, PartyChannel, loading: false);
        await PartyBroadcastAsync(party, refresh).ConfigureAwait(false);
    }

    /// <summary>
    /// On a join, exchanges HP bars between the joiner and their same-map party members so both
    /// sides' windows start correct (ports <c>updatePartyMemberHP</c> + <c>receivePartyMemberHP</c>).
    /// </summary>
    private async ValueTask SyncPartyHpAsync(Party party, FieldPlayer joiner)
    {
        Character jc = joiner.Character;
        byte[] joinerHp = _packets.UserHP(jc.Id, jc.Hp, jc.MaxHp);
        foreach (FieldPlayer member in party.Members)
        {
            if (member.Character.Id == jc.Id || member.Character.MapId != jc.MapId)
            {
                continue;
            }

            await TrySendAsync(member, joinerHp).ConfigureAwait(false);            // member sees joiner
            Character mc = member.Character;
            await TrySendAsync(joiner, _packets.UserHP(mc.Id, mc.Hp, mc.MaxHp)).ConfigureAwait(false); // joiner sees member
        }
    }

    private static async ValueTask TrySendAsync(FieldPlayer player, byte[] packet)
    {
        try
        {
            await player.Session.SendAsync(packet).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A dead session drops out on its own disconnect path; keep fanning out.
        }
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

            case "heal":
            {
                Character hc = _player!.Character;
                hc.Hp = hc.MaxHp;
                hc.Mp = hc.MaxMp;
                await session.SendAsync(_packets.StatChanged(hc, StatFlag.Hp | StatFlag.Mp)).ConfigureAwait(false);
                await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false); // party sees the heal
                break;
            }

            case "warp" when parts.Length >= 2:
            {
                FieldPlayer? target = _fields.FindPlayerByName(parts[1]);
                if (target is null || target.Character.Id == _player!.Character.Id)
                {
                    await ReplyAsync(session, $"'{parts[1]}' is not online").ConfigureAwait(false);
                    break;
                }

                await MovePlayerToMapAsync(session, target.Character.MapId, spawnPortal: 0).ConfigureAwait(false);
                break;
            }

            case "players":
            case "online":
            {
                var names = new List<string>();
                foreach (Field f in _fields.Fields)
                {
                    foreach (FieldPlayer fp in f.Players)
                    {
                        names.Add(fp.Character.Name);
                    }
                }

                await ReplyAsync(session, "online: " + (names.Count == 0 ? "(none)" : string.Join(", ", names)))
                    .ConfigureAwait(false);
                break;
            }

            case "pos":
                await ReplyAsync(session, $"pos: ({_player!.X}, {_player.Y}) map {_player.Character.MapId}")
                    .ConfigureAwait(false);
                break;

            case "help":
                await ReplyAsync(session, "commands: !map <id>, !warp <name>, !meso <n>, !heal, !players, !notice <msg>, !pos, !help")
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
        await NotifyPartyOfMyHpAsync(_player!).ConfigureAwait(false); // party sees the revive
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
        await RefreshPartyWindowAsync(player).ConfigureAwait(false); // party window shows the new map
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
