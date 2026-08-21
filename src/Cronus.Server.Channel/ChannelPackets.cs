using Cronus.Common;
using Cronus.Domain;
using Cronus.Network.Packets;

namespace Cronus.Server.Channel;

/// <summary>
/// Builds channel-stage server packets for JMS v186 (ports <c>ResCStage.SetField</c>, JMS
/// branch, and <c>ResCClientSocket.AliveReq</c>).
/// </summary>
public sealed class ChannelPackets
{
    private readonly OpcodeTable _serverOps;
    private readonly ServerConfig _config;

    public ChannelPackets(OpcodeTable serverOpcodes, ServerConfig config)
    {
        _serverOps = serverOpcodes;
        _config = config;
    }

    /// <summary>
    /// Builds <c>LP_SetField</c> for initial game entry (bCharacterData = true): full
    /// character data plus the random-damage seeds and logout-gift config.
    /// </summary>
    public byte[] SetFieldEnterGame(Character character, int channelId, (int S1, int S2, int S3) damageSeeds)
    {
        PacketWriter w = NewPacket(ServerOpcode.SetField);

        w.WriteShort(0);                  // CClientOptMan::DecodeOpt (JMS >= 186): no entries
        w.WriteInt(channelId);            // m_nChannelID (0-based)
        w.WriteByte(0);                   // (JMS >= 146)
        w.WriteInt(0);                    // m_dwOldDriverID (JMS >= 180)
        w.WriteByte(1);                   // portal count / sNotifierMessage
        w.WriteByte(1);                   // bCharacterData = true
        w.WriteShort(0);                  // nNotifierCheck (JMS >= 146)

        // Random-damage seeds (CalcDamage init).
        w.WriteInt(damageSeeds.S1);
        w.WriteInt(damageSeeds.S2);
        w.WriteInt(damageSeeds.S3);

        CharacterDataEncoder.WriteAllData(w, character);

        // Logout-gift config (JMS >= 186): something + 3 item slots.
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);

        w.WriteLong(CharacterDataEncoder.FileTimeNow()); // ftServer

        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_SetField</c> for a map change (bCharacterData = false): the target map,
    /// spawn portal, and current HP instead of the full character blob.
    /// </summary>
    public byte[] SetFieldChangeMap(Character character, int channelId)
    {
        PacketWriter w = NewPacket(ServerOpcode.SetField);

        w.WriteShort(0);                  // ClientOptMan (JMS >= 186): no entries
        w.WriteInt(channelId);            // m_nChannelID
        w.WriteByte(0);                   // (JMS >= 146)
        w.WriteInt(0);                    // m_dwOldDriverID (JMS >= 180)
        w.WriteByte(1);                   // portal count
        w.WriteByte(0);                   // bCharacterData = false
        w.WriteShort(0);                  // nNotifierCheck

        w.WriteByte(0);                   // clear stat / revive flag (JMS >= 180)
        w.WriteInt(character.MapId);      // dwPosMap
        w.WriteByte(character.Portal);    // nPortal
        w.WriteShort(character.Hp);       // nHP (16-bit, pre-Big-Bang)

        w.WriteLong(CharacterDataEncoder.FileTimeNow()); // ftServer
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_TransferFieldReqIgnored</c> (1 = disabled portal, etc.).</summary>
    public byte[] TransferFieldReqIgnored(byte reason)
    {
        PacketWriter w = NewPacket(ServerOpcode.TransferFieldReqIgnored);
        w.WriteByte(reason);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_ScriptMessage</c> for an NPC dialog line (ports <c>ResCScriptMan.ScriptMessage</c>,
    /// JMS v186 path). <paramref name="param"/> is the v186+ speaker byte (0). SM_SAY includes the
    /// prev/next flags; SM_ASKMENU/ASKYESNO/ASKTEXT/ASKACCEPT carry just the text (plus the
    /// ASKTEXT default/min/max fields).
    /// </summary>
    public byte[] ScriptMessage(int npcId, int messageType, string text, bool prev, bool next)
    {
        PacketWriter w = NewPacket(ServerOpcode.ScriptMessage);
        w.WriteByte(4);              // nSpeakerTypeID (unused)
        w.WriteInt(npcId);           // nSpeakerTemplateID
        w.WriteByte(messageType);    // nMsgType
        w.WriteByte(0);              // param (JMS >= 180)

        switch (messageType)
        {
            case 0: // SM_SAY
                w.WriteString(text);
                w.WriteBool(prev);
                w.WriteBool(next);
                break;
            case 2: // SM_ASKYESNO
            case 5: // SM_ASKMENU
            case 13: // SM_ASKACCEPT
                w.WriteString(text);
                break;
            case 3: // SM_ASKTEXT
                w.WriteString(text);
                w.WriteString(string.Empty); // default
                w.WriteShort(0);             // min length
                w.WriteShort(0);             // max length
                break;
            default:
                w.WriteString(text);
                break;
        }

        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_NpcEnterField</c> spawning an NPC (ports <c>ResCNpcPool.NpcEnterField</c> +
    /// <c>CNpc_Init</c>, JMS v186 path — no JMS &gt;= 194 trailing byte).
    /// </summary>
    public byte[] NpcEnterField(FieldNpc npc)
    {
        PacketWriter w = NewPacket(ServerOpcode.NpcEnterField);
        w.WriteInt(npc.ObjectId);     // dwNpcId (runtime oid)
        w.WriteInt(npc.TemplateId);   // NpcTemplate

        // CNpc::Init
        w.WriteShort(npc.X);
        w.WriteShort(npc.Y);
        w.WriteByte((byte)npc.Facing);
        w.WriteShort((short)npc.Foothold);
        w.WriteShort((short)npc.Rx0);
        w.WriteShort((short)npc.Rx1);
        w.WriteByte(1);               // m_bEnabled
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_MobEnterField</c> spawning a monster (ports <c>ResCMobPool.MobEnterField</c>
    /// + <c>CMob_Init</c>, JMS v186 path: control-normal, a 16-byte all-zero temporary-stat mask,
    /// and the pre-BB init tail — no temporary stats, MOBAPPEAR_NORMAL).
    /// </summary>
    public byte[] MobEnterField(FieldMob mob)
    {
        PacketWriter w = NewPacket(ServerOpcode.MobEnterField);
        w.WriteInt(mob.ObjectId);     // dwMobID (runtime oid)
        w.WriteByte(1);               // 1 = control normal
        w.WriteInt(mob.TemplateId);   // mob template
        WriteMobTemporaryStat(w);
        WriteMobInit(w, mob);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_MobChangeController</c> making the receiving client this mob's local
    /// controller (ports <c>ResCMobPool.MobChangeController</c>, JMS v186; nLevel 1 = control,
    /// 2 = control with aggro).
    /// </summary>
    public byte[] MobChangeController(FieldMob mob, bool aggro = false)
    {
        PacketWriter w = NewPacket(ServerOpcode.MobChangeController);
        w.WriteByte(aggro ? (byte)2 : (byte)1); // nLevel
        w.WriteInt(mob.ObjectId);
        w.WriteByte(1);               // nCalcDamageIndex = control normal
        w.WriteInt(mob.TemplateId);
        WriteMobTemporaryStat(w);
        WriteMobInit(w, mob);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_MobCtrlAck</c> answering a controller's <c>CP_MobMove</c> (ports
    /// <c>ResCMobPool.MobCtrlAck</c>, JMS v186 — no &gt;= 194 tail).
    /// </summary>
    public byte[] MobCtrlAck(FieldMob mob, short moveId, bool aggro, byte skillId = 0, byte skillLevel = 0)
    {
        PacketWriter w = NewPacket(ServerOpcode.MobCtrlAck);
        w.WriteInt(mob.ObjectId);
        w.WriteShort(moveId);
        w.WriteBool(aggro);
        w.WriteShort(mob.Mp);
        w.WriteByte(skillId);
        w.WriteByte(skillLevel);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_MobMove</c> relaying a mob's movement to onlookers (ports
    /// <c>ResCMobPool.MobMove</c>, JMS v186): flags, action, skill, and the raw CMovePath.
    /// </summary>
    public byte[] MobMove(int mobObjectId, bool nextAttackPossible, byte left, int mobSkill, ReadOnlySpan<byte> rawMovePath)
    {
        PacketWriter w = NewPacket(ServerOpcode.MobMove);
        w.WriteInt(mobObjectId);
        w.WriteByte(0);                        // bNotForceLandingWhenDiscard (JMS >= 186)
        w.WriteByte(0);                        // bNotChangeAction
        w.WriteBool(nextAttackPossible);
        w.WriteByte(left);
        w.WriteInt(mobSkill);
        w.WriteInt(0);                         // JMS >= 186 pair
        w.WriteInt(0);
        w.WriteBytes(rawMovePath);
        return w.ToArray();
    }

    /// <summary>CMob::SetTemporaryStat — JMS v186 mask is 4 ints, all zero (no buffs).</summary>
    private static void WriteMobTemporaryStat(PacketWriter w)
    {
        for (int i = 0; i < 4; i++)
        {
            w.WriteInt(0);
        }
    }

    /// <summary>CMob::Init for JMS v186.</summary>
    private static void WriteMobInit(PacketWriter w, FieldMob mob)
    {
        const int spawnStance = 5;

        w.WriteShort(mob.X);
        w.WriteShort(mob.Y);
        w.WriteByte(spawnStance);            // m_nMoveAction
        w.WriteShort((short)mob.Foothold);   // current foothold
        w.WriteShort((short)mob.Foothold);   // origin foothold
        w.WriteByte(unchecked((byte)-1));    // nAppearType = MOBAPPEAR_NORMAL
        w.WriteByte(unchecked((byte)-1));    // m_nTeamForMCarnival
        w.WriteInt(0);                       // nEffectItemID (JMS >= 146)
        w.WriteInt(0);                       // m_nPhase (JMS >= 165)
    }

    /// <summary>
    /// Builds <c>LP_StatChanged</c> for the changed stats in <paramref name="flags"/> (ports
    /// <c>ResCWvsContext.StatChanged</c> + <c>EncodeChangeStat</c>, JMS v186 pre-BB path: 4-byte
    /// mask, 16-bit HP/MP, no pet/extra tail). Values are written in ascending bit order.
    /// </summary>
    public byte[] StatChanged(Character c, StatFlag flags, bool unlock = true)
    {
        PacketWriter w = NewPacket(ServerOpcode.StatChanged);
        w.WriteByte(unlock ? (byte)1 : (byte)0); // bExclRequestSent
        w.WriteInt((int)flags);                  // statmask (JMS < 302 = 4 bytes)

        if (flags.HasFlag(StatFlag.Skin)) w.WriteByte(c.SkinColor);
        if (flags.HasFlag(StatFlag.Face)) w.WriteInt(c.Face);
        if (flags.HasFlag(StatFlag.Hair)) w.WriteInt(c.Hair);
        if (flags.HasFlag(StatFlag.Level)) w.WriteByte(c.Level);
        if (flags.HasFlag(StatFlag.Job)) w.WriteShort(c.Job);
        if (flags.HasFlag(StatFlag.Str)) w.WriteShort(c.Str);
        if (flags.HasFlag(StatFlag.Dex)) w.WriteShort(c.Dex);
        if (flags.HasFlag(StatFlag.Int)) w.WriteShort(c.Int);
        if (flags.HasFlag(StatFlag.Luk)) w.WriteShort(c.Luk);
        if (flags.HasFlag(StatFlag.Hp)) w.WriteShort(c.Hp);       // pre-BB: 16-bit
        if (flags.HasFlag(StatFlag.MaxHp)) w.WriteShort(c.MaxHp);
        if (flags.HasFlag(StatFlag.Mp)) w.WriteShort(c.Mp);
        if (flags.HasFlag(StatFlag.MaxMp)) w.WriteShort(c.MaxMp);
        if (flags.HasFlag(StatFlag.Ap)) w.WriteShort(c.Ap);
        if (flags.HasFlag(StatFlag.Sp)) w.WriteShort(c.Sp);
        if (flags.HasFlag(StatFlag.Exp)) w.WriteInt(c.Exp);
        if (flags.HasFlag(StatFlag.Fame)) w.WriteShort(c.Fame);
        if (flags.HasFlag(StatFlag.Meso)) w.WriteInt(c.Meso);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_BroadcastMsg</c> of type BM_NOTICE (blue "[Notice]" text) or BM_ALERT
    /// (a dialog popup) — both encode just the message (ports <c>ResCWvsContext.BroadcastMsg</c>).
    /// </summary>
    public byte[] BroadcastNotice(string message, bool alert = false)
    {
        const int bmNotice = 0;
        const int bmAlert = 1;

        PacketWriter w = NewPacket(ServerOpcode.BroadcastMsg);
        w.WriteByte(alert ? bmAlert : bmNotice);
        w.WriteString(message);
        return w.ToArray();
    }

    /// <summary>Mirrors a melee attack to onlookers (<c>LP_UserMeleeAttack</c>).</summary>
    public byte[] UserMeleeAttack(int characterId, int level, AttackInfo attack)
        => UserAttack(ServerOpcode.UserMeleeAttack, characterId, level, attack, bulletItemId: 0, x: 0, y: 0, isShoot: false);

    /// <summary>Mirrors a magic attack to onlookers (<c>LP_UserMagicAttack</c>).</summary>
    public byte[] UserMagicAttack(int characterId, int level, AttackInfo attack)
        => UserAttack(ServerOpcode.UserMagicAttack, characterId, level, attack, bulletItemId: 0, x: 0, y: 0, isShoot: false);

    /// <summary>
    /// Mirrors a ranged attack to onlookers (<c>LP_UserShootAttack</c>): as melee, but the bullet
    /// item id is sent and the shooter's position is appended.
    /// </summary>
    public byte[] UserShootAttack(int characterId, int level, AttackInfo attack, int bulletItemId, short x, short y)
        => UserAttack(ServerOpcode.UserShootAttack, characterId, level, attack, bulletItemId, x, y, isShoot: true);

    /// <summary>
    /// Builds an attack-mirror packet (ports <c>ResCUserRemote.UserAttack</c>, JMS v186 pre-BB):
    /// attacker, hit key, level, skill level (0 = basic — no skill id), buff/action/speed, bullet
    /// item id, then per-target damages with a critical-flag byte each; ranged appends the
    /// shooter's position. All three attack kinds share this layout at v186.
    /// </summary>
    private byte[] UserAttack(string opcode, int characterId, int level, AttackInfo attack, int bulletItemId, short x, short y, bool isShoot)
    {
        PacketWriter w = NewPacket(opcode);
        w.WriteInt(characterId);
        w.WriteByte((byte)attack.HitKey);
        w.WriteByte((byte)level);            // m_nLevel (JMS >= 164)
        w.WriteByte((byte)attack.SkillLevel);
        // skillLevel == 0 → no skill id (basic); skill-effect rendering is a follow-up.
        w.WriteByte((byte)attack.BuffKey);
        w.WriteShort((short)attack.AttackActionKey); // JMS > 147
        w.WriteByte((byte)attack.AttackSpeed);
        w.WriteByte(0);                      // nMastery (not modeled)
        w.WriteInt(bulletItemId);            // nBulletItemID (0 for melee/magic)

        foreach (AttackTarget target in attack.Targets)
        {
            w.WriteInt(target.MobObjectId);
            w.WriteByte(7);
            foreach (int damage in target.Damages)
            {
                w.WriteByte((damage & unchecked((int)0x80000000)) != 0 ? (byte)1 : (byte)0); // critical
                w.WriteInt(damage & 0x7FFFFFFF);
            }
        }

        if (isShoot)
        {
            w.WriteShort(x); // shooter position (used by the client to draw the projectile)
            w.WriteShort(y);
        }

        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_MobLeaveField</c> (ports <c>ResCMobPool.MobLeaveField</c>): the mob's object
    /// id and a dead-type animation (1 = killed / fade out).
    /// </summary>
    public byte[] MobLeaveField(int objectId, byte deadType = 1)
    {
        PacketWriter w = NewPacket(ServerOpcode.MobLeaveField);
        w.WriteInt(objectId);
        w.WriteByte(deadType);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_DropEnterField</c> for a meso drop with the drop-from-mob animation
    /// (ports <c>ResCDropPool.DropEnterField</c>, JMS v186; ANIMATION enter type, FFA).
    /// </summary>
    public byte[] DropEnterFieldMeso(FieldDrop drop)
    {
        const int animation = 1; // EnterType.ANIMATION
        const int freeForAll = 2;

        PacketWriter w = NewPacket(ServerOpcode.DropEnterField);
        w.WriteByte(animation);
        w.WriteInt(drop.ObjectId);
        w.WriteByte(1);                  // meso flag
        w.WriteInt(drop.Meso);           // meso amount (in the item-id field)
        w.WriteInt(0);                   // owner (0 = free for all)
        w.WriteByte(freeForAll);         // drop type
        w.WriteShort(drop.X);            // landing x
        w.WriteShort(drop.Y);            // landing y
        w.WriteInt(drop.SourceObjectId); // source mob
        w.WriteShort(drop.SourceX);      // drop-from x (ANIMATION)
        w.WriteShort(drop.SourceY);      // drop-from y
        w.WriteShort(0);
        // meso drops omit the 8-byte expiration.
        w.WriteByte(1);                  // not a player drop
        w.WriteByte(0);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_DropLeaveField</c> for a pickup (ports <c>ResCDropPool.DropLeaveField</c>,
    /// PICK_UP): the leave type, drop id, and the picking character's object id.
    /// </summary>
    public byte[] DropLeaveFieldPickup(int dropObjectId, int pickerCharacterId)
    {
        const int pickUp = 2; // LeaveType.PICK_UP

        PacketWriter w = NewPacket(ServerOpcode.DropLeaveField);
        w.WriteByte(pickUp);
        w.WriteInt(dropObjectId);
        w.WriteInt(pickerCharacterId);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_DropLeaveField</c> for an expired drop that fades on its own (LeaveType.TIMEOUT
    /// = 0 — no owner, so no trailing character id).
    /// </summary>
    public byte[] DropLeaveFieldExpire(int dropObjectId)
    {
        const int timeOut = 0; // LeaveType.TIMEOUT

        PacketWriter w = NewPacket(ServerOpcode.DropLeaveField);
        w.WriteByte(timeOut);
        w.WriteInt(dropObjectId);
        return w.ToArray();
    }

    /// <summary>Windows FILETIME for "2027-07-07" — the magical (effectively permanent) expiration.</summary>
    private const long MagicalExpiration = 134594172000000000L;

    /// <summary>
    /// Builds <c>LP_ChangeSkillRecordResult</c> confirming a skill's new level (ports
    /// <c>ResCWvsContext.ChangeSkillRecordResult</c>, JMS v186).
    /// </summary>
    public byte[] ChangeSkillRecordResult(int skillId, int level, int masterLevel = 0)
    {
        PacketWriter w = NewPacket(ServerOpcode.ChangeSkillRecordResult);
        w.WriteByte(1);
        w.WriteShort(1);              // record count
        w.WriteInt(skillId);
        w.WriteInt(level);
        w.WriteInt(masterLevel);
        w.WriteLong(MagicalExpiration); // JMS >= 164
        w.WriteByte(4);
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_ForcedStatReset</c> (empty) — clears temporary forced stats on entry.</summary>
    public byte[] ForcedStatReset()
        => NewPacket(ServerOpcode.ForcedStatReset).ToArray();

    /// <summary>
    /// Builds <c>LP_FuncKeyMappedInit</c> with the default key layout (ports
    /// <c>ResCFuncKeyMappedMan.FuncKeyMappedInit</c>, JMS v186: 94 slots of [type:1][action:4]).
    /// The client needs its key map to enter the field.
    /// </summary>
    public byte[] FuncKeyMappedInit()
    {
        const int keyMapSize = 94; // JMS pre-Big-Bang
        PacketWriter w = NewPacket(ServerOpcode.FuncKeyMappedInit);
        w.WriteByte(0); // not a reset: the full layout follows

        foreach ((byte type, int action) in DefaultKeyMap(keyMapSize))
        {
            w.WriteByte(type);
            w.WriteInt(action);
        }

        return w.ToArray();
    }

    /// <summary>Builds <c>LP_MacroSysDataInit</c> with no macros.</summary>
    public byte[] MacroSysDataInit()
    {
        PacketWriter w = NewPacket(ServerOpcode.MacroSysDataInit);
        w.WriteByte(0); // macro count
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_PetConsumeItemInit</c> (pet auto-HP-potion item id, 0 = none). Part of the
    /// JMS v186 OnMigrateIn sequence; the client expects the three pet-consume-init packets.
    /// </summary>
    public byte[] PetConsumeItemInit()
    {
        PacketWriter w = NewPacket(ServerOpcode.PetConsumeItemInit);
        w.WriteInt(0); // no auto-consume item
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_PetConsumeMPItemInit</c> (pet auto-MP-potion item id, 0 = none).</summary>
    public byte[] PetConsumeMpItemInit()
    {
        PacketWriter w = NewPacket(ServerOpcode.PetConsumeMpItemInit);
        w.WriteInt(0);
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_JMS_PetConsumeCureItemInit</c> (pet auto-cure item id, 0 = none).</summary>
    public byte[] PetConsumeCureItemInit()
    {
        PacketWriter w = NewPacket(ServerOpcode.PetConsumeCureItemInit);
        w.WriteInt(0);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_FriendResult</c> initialising an empty buddy list on entry (ports
    /// <c>ResCWvsContext.FriendResult</c>, JMS v186: FriendRes_LoadFriend = 7, count 0).
    /// </summary>
    public byte[] FriendListInit()
    {
        PacketWriter w = NewPacket(ServerOpcode.FriendResult);
        w.WriteByte(7); // FriendRes_LoadFriend / LoadFriendDone
        w.WriteByte(0); // friend count
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_FamilyInfoResult</c> for a character with no family (the fixed default the
    /// reference emits on entry: all zero except the pedigree-generation default byte). The large
    /// static <c>LP_FamilyPrivilegeList</c> (0x006C, a version-constant reunion-privilege table)
    /// is not yet ported — the family UI is non-core and this does not affect field entry.
    /// </summary>
    public byte[] FamilyInfoResult()
    {
        PacketWriter w = NewPacket(ServerOpcode.FamilyInfoResult);
        w.WriteInt(0);      // dwFamilyID
        w.WriteInt(0);      // reputation etc.
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteByte(2);     // pedigree-generation default (matches reference)
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteByte(0);
        return w.ToArray();
    }

    /// <summary>
    /// Builds the empty <c>LP_BroadcastMsg</c> slide the client expects at the end of the
    /// OnMigrateIn sequence (BM_SLIDE = 4, disabled → no marquee text).
    /// </summary>
    public byte[] BroadcastSlideClear()
    {
        PacketWriter w = NewPacket(ServerOpcode.BroadcastMsg);
        w.WriteByte(4); // BM_SLIDE
        w.WriteByte(0); // disabled (no text follows)
        return w.ToArray();
    }

    /// <summary>
    /// The classic MapleStory default key bindings (type 4 = skill/action for most). Keys not
    /// listed are unbound (type 0). Returns exactly <paramref name="size"/> slots.
    /// </summary>
    private static IEnumerable<(byte Type, int Action)> DefaultKeyMap(int size)
    {
        // key index -> (type, action). Type 4 = command/skill, 6 = menu, 5 = item, 8 = face.
        var map = new Dictionary<int, (byte, int)>
        {
            [2] = (4, 10), [3] = (4, 12), [4] = (4, 13), [5] = (4, 18), [6] = (4, 24), [7] = (4, 21),
            [16] = (4, 8), [17] = (4, 5), [18] = (4, 0), [19] = (4, 4), [23] = (4, 1), [25] = (4, 19),
            [26] = (4, 14), [27] = (4, 15), [29] = (5, 52), [31] = (6, 2), [33] = (6, 3), [34] = (6, 4),
            [35] = (6, 5), [37] = (6, 6), [38] = (6, 22), [39] = (6, 7), [40] = (4, 20), [41] = (6, 8),
            [43] = (4, 9), [44] = (5, 50), [45] = (5, 51), [46] = (4, 11), [48] = (4, 3), [50] = (4, 16),
            [56] = (4, 2), [57] = (4, 17), [59] = (6, 25), [60] = (5, 53), [61] = (6, 54), [62] = (6, 100),
            [63] = (6, 101), [64] = (6, 102), [65] = (6, 103), [66] = (6, 104), [67] = (6, 105),
        };

        for (int i = 0; i < size; i++)
        {
            yield return map.TryGetValue(i, out (byte, int) b) ? b : ((byte)0, 0);
        }
    }

    /// <summary>Builds <c>LP_AliveReq</c> (keep-alive ping).</summary>
    public byte[] AliveReq()
    {
        PacketWriter w = NewPacket(ServerOpcode.AliveReq);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_UserEnterField</c> announcing a remote player (ports
    /// <c>DataCUserRemote.Init</c>, JMS v186 path: &gt;= 164, &lt; 187, no guild/buffs/pets).
    /// </summary>
    public byte[] UserEnterField(FieldPlayer player)
    {
        Character c = player.Character;
        PacketWriter w = NewPacket(ServerOpcode.UserEnterField);

        w.WriteInt(c.Id);
        w.WriteByte(c.Level);            // JMS >= 164
        w.WriteString(c.Name);
        // Empty guild block.
        w.WriteString(string.Empty);
        w.WriteShort(0);
        w.WriteByte(0);
        w.WriteShort(0);
        w.WriteByte(0);
        w.WriteLong(0);                  // buff mask (JMS >= 164)
        w.WriteLong(0);                  // buff mask
        w.WriteByte(0);                  // energy charge
        w.WriteByte(0);
        w.WriteShort(c.Job);
        Cronus.Server.Login.CharacterEncoder.WriteAvatarLook(w, c);
        w.WriteInt(0);                   // follow character id
        w.WriteInt(0);                   // JMS >= 164 block
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);                   // active effect item
        w.WriteInt(0);                   // chair
        w.WriteShort(player.X);
        w.WriteShort(player.Y);
        w.WriteByte(player.Stance);
        w.WriteShort(0);                 // foothold
        w.WriteByte(0);                  // pet count
        w.WriteInt(0);                   // mount level
        w.WriteInt(0);                   // mount exp
        w.WriteInt(0);                   // mount fatigue
        w.WriteByte(0);                  // mini-room balloon (none)
        w.WriteByte(0);                  // ad board (none)
        w.WriteByte(0);                  // couple records
        w.WriteByte(0);                  // friend records
        w.WriteByte(0);                  // marriage record
        w.WriteByte(0);                  // effect mask
        w.WriteInt(0);                   // m_nPhase
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_UserLeaveField</c>.</summary>
    public byte[] UserLeaveField(int characterId)
    {
        PacketWriter w = NewPacket(ServerOpcode.UserLeaveField);
        w.WriteInt(characterId);
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_UserChat</c> (ports <c>ResCUser.UserChat</c>, JMS v186 path).</summary>
    public byte[] UserChat(int characterId, bool isGm, string message, bool onlyBalloon)
    {
        PacketWriter w = NewPacket(ServerOpcode.UserChat);
        w.WriteInt(characterId);
        w.WriteBool(isGm);
        w.WriteString(message);
        w.WriteBool(onlyBalloon);        // JMS >= 146
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_UserEmotion</c> mirroring a player's face emote to onlookers (ports
    /// <c>ResCUserRemote.UserEmotion</c> + <c>DataCUser.Emotion</c>): character id, the expression,
    /// then the duration (-1) and a trailing byte.
    /// </summary>
    public byte[] UserEmotion(int characterId, int expression)
    {
        PacketWriter w = NewPacket(ServerOpcode.UserEmotion);
        w.WriteInt(characterId);
        w.WriteInt(expression);
        w.WriteInt(-1);                  // duration (unused for the basic emotes)
        w.WriteByte(0);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_UserSitResult</c> (ports <c>ResCUserLocal.UserSitResult</c>): whether the
    /// player is now seated, and the seat id when they are. <paramref name="seatId"/> == -1 stands.
    /// </summary>
    public byte[] UserSitResult(short seatId)
    {
        bool sitting = seatId != -1;

        PacketWriter w = NewPacket(ServerOpcode.UserSitResult);
        w.WriteBool(sitting);
        if (sitting)
        {
            w.WriteShort(seatId);
        }

        return w.ToArray();
    }

    // LP_Whisper flag = req_res | loc_whis (ports Ops_Whisper). req_res: WP_Result=0x08,
    // WP_Receive=0x10; loc_whis: WP_Location=0x01, WP_Whisper=0x02.
    private const int WpLocation = 0x01;
    private const int WpWhisper = 0x02;
    private const int WpResult = 0x08;
    private const int WpReceive = 0x10;

    /// <summary>
    /// Builds the sender-side <c>LP_Whisper</c> (WP_Result | WP_Whisper) that acks a whisper: the
    /// target name echoed back and whether it was delivered (ports <c>ResCField.Whisper</c>).
    /// </summary>
    public byte[] WhisperResult(string targetName, bool delivered)
    {
        PacketWriter w = NewPacket(ServerOpcode.Whisper);
        w.WriteByte((byte)(WpResult | WpWhisper));
        w.WriteString(targetName);
        w.WriteBool(delivered);          // 1 = target online and message delivered
        return w.ToArray();
    }

    /// <summary>
    /// Builds the recipient-side <c>LP_Whisper</c> (WP_Receive | WP_Whisper) that delivers a
    /// whisper: sender name, sender channel (0-based), an admin flag, and the message (ports
    /// <c>ResCField.Whisper</c>).
    /// </summary>
    public byte[] WhisperReceive(string senderName, int senderChannel, string message)
    {
        PacketWriter w = NewPacket(ServerOpcode.Whisper);
        w.WriteByte((byte)(WpReceive | WpWhisper));
        w.WriteString(senderName);
        w.WriteByte((byte)senderChannel);
        w.WriteByte(0);                  // admin?
        w.WriteString(message);
        return w.ToArray();
    }

    /// <summary>
    /// Builds the <c>LP_Whisper</c> location result (WP_Result | WP_Location) that answers the
    /// client's "/find" for <paramref name="targetName"/>. When the target is online on this
    /// channel the result is <c>LR_GameSvr</c> (1) + their map id; otherwise <c>LR_None</c> (0)
    /// + 0 (ports <c>ResCField.Whisper</c> + <c>OpsLocationResult</c>).
    /// </summary>
    public byte[] WhisperLocationResult(string targetName, int mapIdOrZero, bool online)
    {
        const byte LrNone = 0;
        const byte LrGameSvr = 1;

        PacketWriter w = NewPacket(ServerOpcode.Whisper);
        w.WriteByte((byte)(WpResult | WpLocation));
        w.WriteString(targetName);
        w.WriteByte(online ? LrGameSvr : LrNone);
        w.WriteInt(online ? mapIdOrZero : 0);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_UserMove</c> relaying a raw CMovePath buffer (ports
    /// <c>ResCUserRemote.UserMove</c>: character id + the path bytes as received).
    /// </summary>
    public byte[] UserMove(int characterId, ReadOnlySpan<byte> rawMovePath)
    {
        PacketWriter w = NewPacket(ServerOpcode.UserMove);
        w.WriteInt(characterId);
        w.WriteBytes(rawMovePath);
        return w.ToArray();
    }

    private PacketWriter NewPacket(string opcodeName)
        => new(_serverOps.Get(opcodeName), _config.PacketHeaderSize, _config.CodePage);
}
