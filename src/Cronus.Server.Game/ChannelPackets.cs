using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network.Packets;

namespace Cronus.Server.Game;

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
    /// Builds <c>LP_DropEnterField</c> for a meso drop (ports <c>ResCDropPool.DropEnterField</c>,
    /// JMS v186; FFA). Fresh drops use ANIMATION (fall from the source mob); when
    /// <paramref name="onGround"/> the drop is already lying there (NO_ANIMATION, no drop-from
    /// coords) — used to show a newcomer the drops already in the field.
    /// </summary>
    public byte[] DropEnterFieldMeso(FieldDrop drop, bool onGround = false) => DropEnterField(drop, onGround);

    /// <summary>
    /// Builds <c>LP_DropEnterField</c> for an item drop (the item-drop branch of
    /// <c>ResCDropPool.DropEnterField</c>): meso flag 0, the item id, and — unlike meso — an 8-byte
    /// <c>-1</c> (NoExpiration) trailer. The stack count is server-side only (not on the wire); it is
    /// applied to inventory on pickup.
    /// </summary>
    public byte[] DropEnterFieldItem(FieldDrop drop, bool onGround = false) => DropEnterField(drop, onGround);

    /// <summary>
    /// Builds <c>LP_DropEnterField</c> for a meso pile or an item stack (ports
    /// <c>ResCDropPool.DropEnterField</c>, JMS v186; FFA). Fresh drops use ANIMATION (fall from the
    /// source mob); when <paramref name="onGround"/> the drop is already lying there (NO_ANIMATION, no
    /// drop-from coords) — used to show a newcomer the drops already in the field. Item drops carry
    /// an 8-byte expiration (<c>-1</c>); meso drops omit it (<c>drop.getMeso() == 0</c> branch).
    /// </summary>
    public byte[] DropEnterField(FieldDrop drop, bool onGround = false)
    {
        const int animation = 1;   // EnterType.ANIMATION
        const int noAnimation = 2; // EnterType.NO_ANIMATION (already on the ground)
        const int freeForAll = 2;

        PacketWriter w = NewPacket(ServerOpcode.DropEnterField);
        w.WriteByte(onGround ? noAnimation : animation);
        w.WriteInt(drop.ObjectId);
        w.WriteByte(drop.IsMeso ? 1 : 0);              // meso-vs-item flag
        w.WriteInt(drop.IsMeso ? drop.Meso : drop.ItemId); // meso amount or item id (shared field)
        w.WriteInt(0);                   // owner (0 = free for all)
        w.WriteByte(freeForAll);         // drop type
        w.WriteShort(drop.X);            // landing x
        w.WriteShort(drop.Y);            // landing y
        w.WriteInt(drop.SourceObjectId); // source mob
        if (!onGround)
        {
            w.WriteShort(drop.SourceX);  // drop-from x (ANIMATION only)
            w.WriteShort(drop.SourceY);  // drop-from y
            w.WriteShort(0);
        }

        if (!drop.IsMeso)
        {
            w.WriteLong(-1);             // item expiration (NoExpiration); meso omits this
        }

        w.WriteByte(drop.IsPlayerDrop ? 0 : 1); // 0 = thrown by a player, 1 = mob drop
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
    /// Builds <c>LP_FieldEffect</c> of type MobHPTag — the boss HP gauge at the bottom of the screen
    /// (ports <c>ResCField.FieldEffect</c> + <c>OpsFieldEffect.FieldEffect_MobHPTag</c>): the mob's
    /// object id, current HP, max HP, and the two tag colours from its wz data.
    /// </summary>
    public byte[] MobHpTag(FieldMob mob)
    {
        const byte fieldEffectMobHpTag = 5;

        PacketWriter w = NewPacket(ServerOpcode.FieldEffect);
        w.WriteByte(fieldEffectMobHpTag);
        w.WriteInt(mob.ObjectId);
        w.WriteInt(Math.Max(0, mob.Hp));
        w.WriteInt(mob.MaxHp);
        w.WriteByte((byte)mob.TagColor);
        w.WriteByte((byte)mob.TagBgColor);
        return w.ToArray();
    }

    // LP_Message types for JMS v186 (OpsMessage: no per-region remap applies to 148..193, so the
    // enum defaults hold — MS_IncEXPMessage = 3, MS_IncPOPMessage = 5, MS_IncMoneyMessage = 6).
    private const byte MsgDropPickUp = 0; // MS_DropPickUpMessage
    private const byte MsgIncExp = 3;
    private const byte MsgIncPop = 5;
    private const byte MsgIncMoney = 6;

    /// <summary>
    /// Builds the "+N fame" floating message (<c>LP_Message</c> / MS_IncPOPMessage) shown when a
    /// player's fame changes (ports <c>ResCWvsContext.Message</c>): just the signed delta.
    /// </summary>
    public byte[] IncPopMessage(int fameDelta)
    {
        PacketWriter w = NewPacket(ServerOpcode.Message);
        w.WriteByte(MsgIncPop);
        w.WriteInt(fameDelta);
        return w.ToArray();
    }

    /// <summary>
    /// Builds the "+N exp" floating message (<c>LP_Message</c> / MS_IncEXPMessage) shown on a kill
    /// (ports <c>ResCWvsContext.Message</c>, JMS v186 path). All the bonus fields (party/equip/event/
    /// wedding/rainbow) are zero — the simplified server doesn't model those bonuses.
    /// </summary>
    public byte[] IncExpMessage(int exp)
    {
        PacketWriter w = NewPacket(ServerOpcode.Message);
        w.WriteByte(MsgIncExp);
        w.WriteByte(0);        // nTextColor (white)
        w.WriteInt(exp);       // gained exp
        w.WriteByte(0);        // bOnQuest / in-chat
        w.WriteInt(0);
        w.WriteByte(0);        // nMobEventBonusPercentage (0 -> no play-time byte follows)
        w.WriteByte(0);
        w.WriteInt(0);         // wedding bonus
        w.WriteInt(0);         // group ring bonus
        w.WriteByte(0);        // nPartyBonusEventRate
        w.WriteInt(0);         // party bonus exp
        w.WriteInt(0);         // equipment bonus exp
        w.WriteInt(0);
        w.WriteInt(0);         // rainbow-week bonus exp
        return w.ToArray();
    }

    /// <summary>
    /// Builds the "+N mesos" floating message (<c>LP_Message</c> / MS_IncMoneyMessage, JMS v186 path:
    /// just the amount) shown when meso is gained (ports <c>ResCWvsContext.Message</c>).
    /// </summary>
    public byte[] IncMoneyMessage(int meso)
    {
        PacketWriter w = NewPacket(ServerOpcode.Message);
        w.WriteByte(MsgIncMoney);
        w.WriteInt(meso);
        return w.ToArray();
    }

    /// <summary>
    /// Builds the "obtained &lt;item&gt; x N" floating message (<c>LP_Message</c> /
    /// MS_DropPickUpMessage / PICKUP_ITEM) shown when an item is picked up (ports
    /// <c>ResCWvsContext.Message</c> PICKUP_ITEM branch, JMS v186): the item id and the count gained.
    /// </summary>
    public byte[] ShowItemGain(int itemId, int quantity)
    {
        const byte pickUpItem = 0; // OpsDropPickUpMessage.PICKUP_ITEM

        PacketWriter w = NewPacket(ServerOpcode.Message);
        w.WriteByte(MsgDropPickUp);
        w.WriteByte(pickUpItem);
        w.WriteInt(itemId);
        w.WriteInt(quantity);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_CharacterInfo</c> — another player's info window (ports
    /// <c>ResCWvsContext.CharacterInfo</c>, JMS v186 path). Carries id/level/job/fame, then the
    /// community/pet/mount/wishlist/monster-book/medal/chair blocks. Cronus models none of the latter
    /// yet, so they encode as their empty forms; the guild "community" is <c>"-"</c> (the reference
    /// never actually fills it in). The layout is fixed and golden-tested.
    /// </summary>
    public byte[] CharacterInfo(Character c)
    {
        PacketWriter w = NewPacket(ServerOpcode.CharacterInfo);
        w.WriteInt(c.Id);
        w.WriteByte(c.Level);
        w.WriteShort(c.Job);
        w.WriteShort(c.Fame);
        w.WriteByte(0);              // bIsMarried (JMS >= 147)
        w.WriteString("-");          // sCommunity (guild name; the reference always leaves it "-")
        w.WriteString(string.Empty); // sAlliance (JMS >= 147)
        w.WriteInt(0);               // JMS 180-186 pair
        w.WriteInt(0);
        w.WriteByte(0);              // bPetActivated (no pet)
        w.WriteByte(0);              // SetPetInfo: slot 0 empty terminates the list
        w.WriteByte(0);              // taming-mob enabled
        w.WriteByte(0);              // wishlist size

        // MonsterBookInfo (level, normal, special, total, coverMobId) — all zero.
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);

        // Medal / achievement (JMS >= 180): equipped medal id, then the medal-quest count.
        w.WriteInt(0);
        w.WriteShort(0);

        // Chair list (JMS 180-186): the SETUP-inventory chair count (empty).
        w.WriteInt(0);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_InventoryOperation</c> — live inventory changes (add / update-quantity / remove)
    /// so the client updates without a relog (ports <c>ResCWvsContext.InventoryOperation</c>, JMS
    /// v186). Add carries the full item body; update carries the new quantity; remove just the slot.
    /// </summary>
    public byte[] InventoryOperation(IReadOnlyList<InventoryChange> changes)
    {
        PacketWriter w = NewPacket(ServerOpcode.InventoryOperation);
        w.WriteByte(1);                        // unlock (m_bExclRequestSent)
        w.WriteByte((byte)changes.Count);
        // JMS >= 302 writes an extra byte here; v186 does not.

        foreach (InventoryChange ch in changes)
        {
            w.WriteByte((byte)ch.Mode);
            w.WriteByte((byte)ch.Tab);
            switch (ch.Mode)
            {
                case InvMode.Add:
                    w.WriteShort(ch.Position);
                    Cronus.Server.Login.ItemEncoder.WriteItem(w, ch.Item!);
                    break;
                case InvMode.Update:
                    w.WriteShort(ch.Position);
                    w.WriteShort(ch.Quantity);
                    break;
                case InvMode.Move:
                    w.WriteShort(ch.Position);      // source (old) slot
                    w.WriteShort(ch.DestPosition);  // destination (new) slot
                    break;
                case InvMode.Remove:
                    w.WriteShort(ch.Position);
                    break;
            }
        }

        // An equipped-slot change (a move to/from a negative slot, or a remove of an equipped item)
        // needs one trailing byte for CUserLocal::SetSecondaryStatChangedPoint (ports the JMS v186
        // ResCWvsContext.InventoryOperation trailer).
        bool equipChange = changes.Any(ch =>
            (ch.Mode == InvMode.Move && (ch.Position < 0 || ch.DestPosition < 0)) ||
            (ch.Mode == InvMode.Remove && ch.Position < 0));
        if (equipChange)
        {
            w.WriteByte(0);
        }

        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_FuncKeyMappedInit</c> — restores the player's key layout on game entry (ports
    /// <c>ResCFuncKeyMappedMan.FuncKeyMappedInit</c>, JMS v186): a leading mode byte (0 = full map)
    /// then 94 positional <c>[type:1][action:4]</c> slots (unbound = 0/0).
    /// </summary>
    public byte[] FuncKeyMappedInit(Keymap keymap)
    {
        PacketWriter w = NewPacket(ServerOpcode.FuncKeyMappedInit);
        w.WriteByte(0); // mode 0 = send the full saved map (mode 1 would ask the client to use its default)
        for (int i = 0; i < Keymap.KeyCount; i++)
        {
            if (keymap.Get(i) is { } binding)
            {
                w.WriteByte(binding.Type);
                w.WriteInt(binding.Action);
            }
            else
            {
                w.WriteByte(0);
                w.WriteInt(0);
            }
        }

        return w.ToArray();
    }

    /// <summary>Builds <c>LP_SkillUseResult</c> — acks a skill cast (ports
    /// <c>ResCWvsContext.SkillUseResult</c>, JMS v186: a single unused byte).</summary>
    public byte[] SkillUseResult()
    {
        PacketWriter w = NewPacket(ServerOpcode.SkillUseResult);
        w.WriteByte(0);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_TemporaryStatSet</c> — applies a temporary stat buff to the local player (ports
    /// <c>ResCWvsContext.TemporaryStatSet</c>, JMS v186): the 128-bit CTS mask (4 dwords in reverse
    /// word order), then per active stat (ascending bit order) <c>[value:2][reason=-itemId:4]
    /// [durationMs:4]</c>, then the 5-byte tail (nDefenseAtt, nDefenseState, delay:2, changed-point).
    /// </summary>
    public byte[] TemporaryStatSet(IReadOnlyList<BuffStat> stats)
    {
        PacketWriter w = NewPacket(ServerOpcode.TemporaryStatSet);
        w.WriteInt(0);                                 // mask word[3]
        w.WriteInt(0);                                 // mask word[2]
        w.WriteInt(0);                                 // mask word[1]
        w.WriteInt((int)BuffEffect.Word0Mask(stats));  // mask word[0]
        foreach (BuffStat s in stats)
        {
            w.WriteShort(s.Value);       // nValue (2 bytes in v186)
            w.WriteInt(s.Reason);        // rReason = -itemId (item buff) / +skillId (skill buff)
            w.WriteInt(s.DurationMs);    // tDuration (ms)
        }

        w.WriteByte(0);   // nDefenseAtt
        w.WriteByte(0);   // nDefenseState
        w.WriteShort(0);  // delay
        w.WriteByte(0);   // SetSecondaryStatChangedPoint
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_TemporaryStatReset</c> — clears the given CTS mask (ports
    /// <c>ResCWvsContext.TemporaryStatReset</c>, JMS v186): the 128-bit mask (reverse word order) and
    /// a trailing 0 byte. <paramref name="word0Mask"/> holds the simple-stat bits (word[0]).
    /// </summary>
    public byte[] TemporaryStatReset(uint word0Mask)
    {
        PacketWriter w = NewPacket(ServerOpcode.TemporaryStatReset);
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt((int)word0Mask);
        w.WriteByte(0);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_OpenShopDlg</c> — the NPC shop window (ports <c>ResCShopDlg.OpenShopDlg</c>, JMS
    /// v186): the NPC template id, the item count, then per item <c>[itemId][price][reqItem][reqItemQ]
    /// [period=0][levelLimit=0]</c> followed by an 8-byte <c>double</c> unit price for rechargeables
    /// or the constant quantity <c>1</c> (short) otherwise, then the wz <c>slotMax</c>.
    /// </summary>
    public byte[] OpenShopDlg(Shop shop, IItemProvider items)
    {
        PacketWriter w = NewPacket(ServerOpcode.OpenShopDlg);
        w.WriteInt(shop.NpcId);
        w.WriteShort((short)shop.Items.Count);
        foreach (ShopItem item in shop.Items)
        {
            w.WriteInt(item.ItemId);
            w.WriteInt(item.Price);
            w.WriteInt(item.ReqItem);   // token-shop currency item (JMS >= 180)
            w.WriteInt(item.ReqItemQ);  // token-shop currency amount
            w.WriteInt(0);              // nItemPeriod (JMS >= 186)
            w.WriteInt(0);              // nLevelLimited (JMS >= 180)
            if (ShopItems.IsRechargeable(item.ItemId))
            {
                double unit = items.GetPrice(item.ItemId) ?? item.Price;
                w.WriteLong(BitConverter.DoubleToInt64Bits(unit)); // EncodeDouble (LE)
            }
            else
            {
                w.WriteShort(1);        // nQuantity constant
            }

            w.WriteShort((short)ShopSlotMax(items, item.ItemId));
        }

        return w.ToArray();
    }

    /// <summary>The stack limit shown for a shop item (wz <c>slotMax</c>; equips 1, else 100).</summary>
    private static int ShopSlotMax(IItemProvider items, int itemId)
        => items.GetConsume(itemId)?.SlotMax ?? (itemId / 1000000 == 1 ? 1 : 100);

    /// <summary>Builds <c>LP_ShopResult</c> — a one-byte buy/sell/recharge result (ports
    /// <c>ResCShopDlg.ShopResult</c>, JMS v186: just the result code).</summary>
    public byte[] ShopResult(ShopResultCode code)
    {
        PacketWriter w = NewPacket(ServerOpcode.ShopResult);
        w.WriteByte((byte)code);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_TrunkResult</c> opening the storage window (ports <c>ResCTrunkDlg.TrunkResult</c>
    /// / <c>SetTrunkDlg</c>, op <c>OpenTrunkDlg</c>=21, JMS v186): the op, the NPC id, then the full
    /// dump — slot count, the 8-byte DBCHAR mask (all), the stored meso, and each item category
    /// (count + items).
    /// </summary>
    public byte[] TrunkOpen(int npcId, Storage storage)
    {
        PacketWriter w = NewPacket(ServerOpcode.TrunkResult);
        w.WriteByte((byte)TrunkOp.OpenTrunkDlg);
        w.WriteInt(npcId);
        WriteTrunkItems(w, storage, TrunkMask.All);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_TrunkResult</c> for a deposit/withdraw success (ports the <c>SetGetItems</c>
    /// "last modified" path): the op then only the affected item category (slot count, mask = that
    /// category's bit, and its item list).
    /// </summary>
    public byte[] TrunkItemResult(TrunkOp op, Storage storage, int tab)
    {
        PacketWriter w = NewPacket(ServerOpcode.TrunkResult);
        w.WriteByte((byte)op);
        WriteTrunkItems(w, storage, TrunkMask.CategoryBit(tab));
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_TrunkResult</c> for a meso change (op <c>MoneySuccess</c>=18): slot count,
    /// mask = MONEY, and the new stored meso.</summary>
    public byte[] TrunkMoneyResult(Storage storage)
    {
        PacketWriter w = NewPacket(ServerOpcode.TrunkResult);
        w.WriteByte((byte)TrunkOp.MoneySuccess);
        WriteTrunkItems(w, storage, TrunkMask.Money);
        return w.ToArray();
    }

    /// <summary>Builds a bodiless <c>LP_TrunkResult</c> error (e.g. PutNoSpace / PutNoMoney): just the
    /// op code (ports the <c>default</c> case that emits no body).</summary>
    public byte[] TrunkError(TrunkOp op)
    {
        PacketWriter w = NewPacket(ServerOpcode.TrunkResult);
        w.WriteByte((byte)op);
        return w.ToArray();
    }

    private static void WriteTrunkItems(PacketWriter w, Storage storage, long mask)
    {
        w.WriteByte((byte)storage.Slots);
        w.WriteLong(mask);
        if ((mask & TrunkMask.Money) != 0)
        {
            w.WriteInt(storage.Meso);
        }

        WriteTrunkCategory(w, storage, mask, TrunkMask.Equip, 1);
        WriteTrunkCategory(w, storage, mask, TrunkMask.Consume, 2);
        WriteTrunkCategory(w, storage, mask, TrunkMask.Install, 3);
        WriteTrunkCategory(w, storage, mask, TrunkMask.Etc, 4);
        WriteTrunkCategory(w, storage, mask, TrunkMask.Cash, 5);
    }

    private static void WriteTrunkCategory(PacketWriter w, Storage storage, long mask, long bit, int tab)
    {
        if ((mask & bit) == 0)
        {
            return;
        }

        List<InventoryItem> items = storage.Items
            .Where(i => Cronus.Server.Login.ItemEncoder.ItemType(i.ItemId) == tab)
            .ToList();
        w.WriteByte((byte)items.Count);
        foreach (InventoryItem it in items)
        {
            Cronus.Server.Login.ItemEncoder.WriteItem(w, it);
        }
    }

    // LP_MiniRoom / CP_MiniRoom protocol ops for JMS v186 (OpsMiniRoomProtocol.init, >=186 branch).
    public const byte MiniRoomCreate = 0;
    public const byte MiniRoomInvite = 2;
    public const byte MiniRoomInviteResult = 3;
    public const byte MiniRoomEnter = 4;
    public const byte MiniRoomEnterResult = 5;
    public const byte MiniRoomChat = 6;
    public const byte MiniRoomUserChat = 8;
    public const byte MiniRoomLeave = 10;
    public const byte TradePutItem = 13;
    public const byte TradePutMoney = 14;
    public const byte TradeConfirm = 15;

    // TradeLeave message codes (ResCMiniRoomBaseDlg.TradeMessage).
    public const byte TradeMsgCancelled = 2;
    public const byte TradeMsgSuccess = 7;

    private const byte MiniRoomTypeTrade = 3;

    /// <summary>
    /// Builds the trade-room open packet (<c>LP_MiniRoom</c> / MRP_EnterResult, ports
    /// <c>ResCMiniRoomBaseDlg.getTradeStart</c>): room type 3, capacity 2, then — for the joining
    /// visitor — the starter's block (slot 0) before the recipient's own block, terminated by 0xFF.
    /// </summary>
    public byte[] TradeStart(Character self, byte myNumber, Character? partner)
    {
        PacketWriter w = NewPacket(ServerOpcode.MiniRoom);
        w.WriteByte(MiniRoomEnterResult);
        w.WriteByte(MiniRoomTypeTrade);
        w.WriteByte(2);          // max users
        w.WriteByte(myNumber);
        if (myNumber == 1 && partner is not null)
        {
            w.WriteByte(0);      // the starter occupies slot 0
            Cronus.Server.Login.CharacterEncoder.WriteAvatarLook(w, partner);
            w.WriteString(partner.Name);
            w.WriteShort(partner.Job); // JMS >= 186
        }

        w.WriteByte(myNumber);
        Cronus.Server.Login.CharacterEncoder.WriteAvatarLook(w, self);
        w.WriteString(self.Name);
        w.WriteShort(self.Job);
        w.WriteByte(0xFF);
        return w.ToArray();
    }

    /// <summary>Builds the trade invitation shown to the invitee (ports <c>getTradeInvite</c>).</summary>
    public byte[] TradeInvite(string inviterName)
    {
        PacketWriter w = NewPacket(ServerOpcode.MiniRoom);
        w.WriteByte(MiniRoomInvite);
        w.WriteByte(MiniRoomTypeTrade);
        w.WriteString(inviterName);
        w.WriteInt(0);           // trade id
        return w.ToArray();
    }

    /// <summary>Tells the starter their partner entered the room (ports <c>getTradePartnerAdd</c>).</summary>
    public byte[] TradePartnerAdd(Character joiner)
    {
        PacketWriter w = NewPacket(ServerOpcode.MiniRoom);
        w.WriteByte(MiniRoomEnter);
        w.WriteByte(1);          // the visitor occupies slot 1
        Cronus.Server.Login.CharacterEncoder.WriteAvatarLook(w, joiner);
        w.WriteString(joiner.Name);
        w.WriteShort(joiner.Job);
        return w.ToArray();
    }

    /// <summary>An item staged on a trade side (ports <c>getTradeItemAdd</c>; side is relative — 0 =
    /// the recipient's own side, 1 = the partner's). The item's Position is its trade slot.</summary>
    public byte[] TradeItemAdd(byte side, InventoryItem item)
    {
        PacketWriter w = NewPacket(ServerOpcode.MiniRoom);
        w.WriteByte(TradePutItem);
        w.WriteByte(side);
        w.WriteByte((byte)item.Position);
        Cronus.Server.Login.ItemEncoder.WriteItem(w, item);
        return w.ToArray();
    }

    /// <summary>The total meso staged on a trade side (ports <c>getTradeMesoSet</c>; relative side).</summary>
    public byte[] TradeMesoSet(byte side, int totalMeso)
    {
        PacketWriter w = NewPacket(ServerOpcode.MiniRoom);
        w.WriteByte(TradePutMoney);
        w.WriteByte(side);
        w.WriteInt(totalMeso);
        return w.ToArray();
    }

    /// <summary>The partner pressed Trade (ports <c>getTradeConfirmation</c>).</summary>
    public byte[] TradeConfirmation()
    {
        PacketWriter w = NewPacket(ServerOpcode.MiniRoom);
        w.WriteByte(TradeConfirm);
        return w.ToArray();
    }

    /// <summary>Closes the trade room with a message code (ports <c>TradeMessage</c>: 2 = cancelled,
    /// 7 = success). <paramref name="slot"/> is the recipient's absolute room slot.</summary>
    public byte[] TradeLeave(byte slot, byte message)
    {
        PacketWriter w = NewPacket(ServerOpcode.MiniRoom);
        w.WriteByte(MiniRoomLeave);
        w.WriteByte(slot);
        w.WriteByte(message);
        return w.ToArray();
    }

    /// <summary>A chat line inside the room (ports <c>shopChat</c>): the speaker's slot + text.</summary>
    public byte[] TradeChat(byte speakerSlot, string text)
    {
        PacketWriter w = NewPacket(ServerOpcode.MiniRoom);
        w.WriteByte(MiniRoomChat);
        w.WriteByte(MiniRoomUserChat);
        w.WriteByte(speakerSlot);
        w.WriteString(text);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_UserAvatarModified</c> (ports <c>ResCUserRemote.UserAvatarModified</c>, JMS v186):
    /// the character id, the avatar-change flag, then the full avatar-look block so an equip change
    /// repaints on other players' screens. Broadcast to the field (not the acting player, who already
    /// saw the change via <c>LP_InventoryOperation</c>).
    /// </summary>
    public byte[] UserAvatarModified(Character c)
    {
        PacketWriter w = NewPacket(ServerOpcode.UserAvatarModified);
        w.WriteInt(c.Id);
        w.WriteByte(1);              // flag: 0x01 = avatar look changed
        Cronus.Server.Login.CharacterEncoder.WriteAvatarLook(w, c);
        w.WriteByte(0);             // couple ring
        w.WriteByte(0);             // friendship ring
        w.WriteByte(0);             // marriage ring
        w.WriteInt(0);              // m_nCompletedSetItemID
        return w.ToArray();
    }

    /// <summary>User effect type: the level-up show (ports <c>OpsUserEffect.UserEffect_LevelUp</c>).</summary>
    public const byte UserEffectLevelUp = 0x00;

    /// <summary>User effect type: the quest-complete jingle (JMS v186 <c>OpsUserEffect</c> = 10).</summary>
    public const byte UserEffectQuestComplete = 10;

    /// <summary>Builds <c>LP_UserEffectLocal</c> — plays an effect for the player themself (ports
    /// <c>ResCUserLocal.EffectData</c>; simple effects carry only the type byte).</summary>
    public byte[] UserEffectLocal(byte effectType)
    {
        PacketWriter w = NewPacket(ServerOpcode.UserEffectLocal);
        w.WriteByte(effectType);
        return w.ToArray();
    }

    // OpsQuestRecordMessage states (JMS v186): the quest-record entry's status byte.
    public const byte QuestRecordNone = 0;      // removed / forfeited
    public const byte QuestRecordStarted = 1;   // in progress (carries the progress string)
    public const byte QuestRecordCompleted = 2; // done (carries the completion FILETIME)

    /// <summary>
    /// Builds the quest-journal update (<c>LP_Message</c> / MS_QuestRecordMessage, ports
    /// <c>ResCWvsContext.Message</c> + <c>ResWrapper.updateQuest</c>): quest id, the record state,
    /// then per state — started carries the progress string (e.g. per-mob 3-digit kill counts),
    /// completed the completion FILETIME, and none a single 0 byte.
    /// </summary>
    public byte[] QuestRecordMessage(int questId, byte state, string progress = "")
    {
        const byte msgQuestRecord = 1; // MS_QuestRecordMessage

        PacketWriter w = NewPacket(ServerOpcode.Message);
        w.WriteByte(msgQuestRecord);
        w.WriteShort((short)questId);
        w.WriteByte(state);
        switch (state)
        {
            case QuestRecordStarted:
                w.WriteString(progress);
                break;
            case QuestRecordCompleted:
                w.WriteLong(CharacterDataEncoder.FileTimeNow());
                break;
            default:
                w.WriteByte(0);
                break;
        }

        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_UserQuestResult</c> confirming a quest act succeeded (ports
    /// <c>ResCUserLocal.UserQuestResult</c>; JMS v186 <c>QuestRes_Act_Success</c> = 8): the quest,
    /// the NPC, and the auto-started follow-up quest (0 = none).
    /// </summary>
    public byte[] UserQuestResult(int questId, int npcId, short nextQuest = 0)
    {
        const byte actSuccess = 8;

        PacketWriter w = NewPacket(ServerOpcode.UserQuestResult);
        w.WriteByte(actSuccess);
        w.WriteShort((short)questId);
        w.WriteInt(npcId);
        w.WriteShort(nextQuest);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_UserEffectRemote</c> so onlookers see another player's effect — used for the
    /// level-up animation (ports <c>ResCUserRemote.UserEffectRemote</c> + <c>EffectData</c>):
    /// character id then the effect type. Level-up (type 0) carries no extra payload.
    /// </summary>
    public byte[] UserEffectRemote(int characterId, byte effectType)
    {
        PacketWriter w = NewPacket(ServerOpcode.UserEffectRemote);
        w.WriteInt(characterId);
        w.WriteByte(effectType);
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

    // LP_Messenger sub-operations (ports OpsMessenger). The first byte selects the shape.
    private const byte MsmpEnter = 0;
    private const byte MsmpSelfEnterResult = 1;
    private const byte MsmpLeave = 2;
    private const byte MsmpInvite = 3;
    private const byte MsmpInviteResult = 4;
    private const byte MsmpChat = 6;

    /// <summary>
    /// Builds <c>LP_Messenger</c> MSMP_SelfEnterResult, telling the player who just joined which
    /// slot (0..2) they occupy in the 3-person window (ports <c>ResCUIMessenger.Messenger</c>).
    /// </summary>
    public byte[] MessengerSelfEnterResult(int slotIndex)
    {
        PacketWriter w = NewPacket(ServerOpcode.Messenger);
        w.WriteByte(MsmpSelfEnterResult);
        w.WriteByte((byte)slotIndex);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_Messenger</c> MSMP_Enter describing a member (their slot, avatar look, name,
    /// 0-based channel, and whether they are the one who just joined) so the recipient's window
    /// shows them (ports <c>ResCUIMessenger.Messenger</c> + <c>DataAvatarLook</c>). Reuses the
    /// client-verified avatar-look encoding from the field spawn packet.
    /// </summary>
    public byte[] MessengerEnter(int slotIndex, Character member, int channel, bool isNew)
    {
        PacketWriter w = NewPacket(ServerOpcode.Messenger);
        w.WriteByte(MsmpEnter);
        w.WriteByte((byte)slotIndex);
        Cronus.Server.Login.CharacterEncoder.WriteAvatarLook(w, member);
        w.WriteString(member.Name);
        w.WriteByte((byte)channel);
        w.WriteBool(isNew);
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_Messenger</c> MSMP_Leave: the slot a departing member vacated.</summary>
    public byte[] MessengerLeave(int slotIndex)
    {
        PacketWriter w = NewPacket(ServerOpcode.Messenger);
        w.WriteByte(MsmpLeave);
        w.WriteByte((byte)slotIndex);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_Messenger</c> MSMP_Invite delivered to an invited player: the inviter's name,
    /// 0-based channel, and the messenger id to join.
    /// </summary>
    public byte[] MessengerInvite(string inviterName, int inviterChannel, int messengerId)
    {
        PacketWriter w = NewPacket(ServerOpcode.Messenger);
        w.WriteByte(MsmpInvite);
        w.WriteString(inviterName);
        w.WriteByte((byte)inviterChannel);
        w.WriteInt(messengerId);
        w.WriteByte(0);
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>LP_Messenger</c> MSMP_InviteResult echoed to the members: the invited name and
    /// whether they could be invited (online and not already in a messenger).
    /// </summary>
    public byte[] MessengerInviteResult(string inviteeName, bool found)
    {
        PacketWriter w = NewPacket(ServerOpcode.Messenger);
        w.WriteByte(MsmpInviteResult);
        w.WriteString(inviteeName);
        w.WriteBool(found);
        return w.ToArray();
    }

    /// <summary>Builds <c>LP_Messenger</c> MSMP_Chat: a line for the other members' windows.</summary>
    public byte[] MessengerChat(string message)
    {
        PacketWriter w = NewPacket(ServerOpcode.Messenger);
        w.WriteByte(MsmpChat);
        w.WriteString(message);
        return w.ToArray();
    }

    // LP_PartyResult sub-operations (ports OpsParty). The first byte selects the shape.
    private const byte PartyInviteToInvitee = 4;   // PartyReq_InviteParty (the invite popup)
    private const byte PartyLoadDoneOp = 7;         // PartyRes_LoadParty_Done / silent update
    private const byte PartyCreateDoneOp = 8;       // PartyRes_CreateNewParty_Done
    private const byte PartyDepartOp = 12;          // leave / expel / disband
    private const byte PartyJoinOp = 15;            // PartyRes_JoinParty (someone joined)
    private const byte PartyInviteSentOp = 22;      // PartyRes_InviteParty_Sent
    private const byte PartyChangeLeaderOp = 31;    // PartyRes_ChangePartyBoss_Done

    /// <summary>
    /// Builds a bare <c>LP_PartyResult</c> that carries only its op byte — the many acknowledgement
    /// and error codes (already-joined, full, unknown-user, …) that have no payload.
    /// </summary>
    public byte[] PartyResultSimple(int op)
    {
        PacketWriter w = NewPacket(ServerOpcode.PartyResult);
        w.WriteByte((byte)op);
        return w.ToArray();
    }

    /// <summary>Op byte of <c>PartyRes_CreateNewParty_AlreayJoined</c> (already in a party).</summary>
    public const int PartyErrAlreadyJoined = 9;

    /// <summary>Op byte of <c>PartyRes_WithdrawParty_Unknown</c> (not in a party).</summary>
    public const int PartyErrWithdrawUnknown = 14;

    /// <summary>Op byte of <c>PartyRes_JoinParty_AlreadyFull</c> (party is full / invite failed).</summary>
    public const int PartyErrFull = 18;

    /// <summary>Op byte of <c>PartyRes_JoinParty_UnknownUser</c> (invited name not online).</summary>
    public const int PartyErrUnknownUser = 20;

    /// <summary>Op byte of <c>PartyRes_JoinParty_Unknown</c> (bad party id on join).</summary>
    public const int PartyErrJoinUnknown = 21;

    /// <summary>Op byte of <c>PartyRes_JoinParty_AlreadyJoined</c> (target/self already partied).</summary>
    public const int PartyErrAlreadyInParty = 17;

    /// <summary>Op byte of <c>PartyRes_KickParty_Unknown</c> (not leader / can't kick).</summary>
    public const int PartyErrKickUnknown = 30;

    /// <summary>Op byte of <c>PartyRes_ChangePartyBoss_Unknown</c> (not leader / bad target).</summary>
    public const int PartyErrChangeBossUnknown = 35;

    /// <summary>
    /// Builds <c>PartyRes_CreateNewParty_Done</c> handed to the party's creator: the new party id
    /// then the "no town door" placeholder block (ports <c>ResCWvsContext.PartyResult</c>).
    /// </summary>
    public byte[] PartyCreateDone(int partyId)
    {
        PacketWriter w = NewPacket(ServerOpcode.PartyResult);
        w.WriteByte(PartyCreateDoneOp);
        w.WriteInt(partyId);
        w.WriteInt(PartyMemberView.NoDoor);
        w.WriteInt(PartyMemberView.NoDoor);
        w.WriteLong(0);
        return w.ToArray();
    }

    /// <summary>
    /// Builds the invite popup (<c>PartyReq_InviteParty</c>) delivered to the invited player: the
    /// party id and the inviter's name / level / job (JMS &gt;= 186 sends the job).
    /// </summary>
    public byte[] PartyInvite(int partyId, string inviterName, int inviterLevel, int inviterJob)
    {
        PacketWriter w = NewPacket(ServerOpcode.PartyResult);
        w.WriteByte(PartyInviteToInvitee);
        w.WriteInt(partyId);
        w.WriteString(inviterName);
        w.WriteInt(inviterLevel);
        w.WriteInt(inviterJob);          // JMS >= 186
        w.WriteByte(0);                  // auto-join
        return w.ToArray();
    }

    /// <summary>Builds <c>PartyRes_InviteParty_Sent</c> echoed to the inviter (the invited name).</summary>
    public byte[] PartyInviteSent(string invitedName)
    {
        PacketWriter w = NewPacket(ServerOpcode.PartyResult);
        w.WriteByte(PartyInviteSentOp);
        w.WriteString(invitedName);
        return w.ToArray();
    }

    /// <summary>
    /// Builds a party-window refresh: <c>PartyRes_LoadParty_Done</c> when <paramref name="loading"/>,
    /// else the silent update. Both are op 7: the party id then the member-status block.
    /// </summary>
    public byte[] PartyRefresh(int partyId, IReadOnlyList<PartyMemberView> slots, int leaderId, int forChannel, bool loading)
    {
        PacketWriter w = NewPacket(ServerOpcode.PartyResult);
        w.WriteByte(PartyLoadDoneOp);
        w.WriteInt(partyId);
        WritePartyStatus(w, slots, leaderId, forChannel, leaving: loading); // LoadParty uses leaving=true
        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>PartyRes_JoinParty</c> (op 15) sent to every member when someone joins: the party
    /// id, the joiner's name, and the refreshed member-status block.
    /// </summary>
    public byte[] PartyJoin(int partyId, string joinerName, IReadOnlyList<PartyMemberView> slots, int leaderId, int forChannel)
    {
        PacketWriter w = NewPacket(ServerOpcode.PartyResult);
        w.WriteByte(PartyJoinOp);
        w.WriteInt(partyId);
        w.WriteString(joinerName);
        WritePartyStatus(w, slots, leaderId, forChannel, leaving: false);
        return w.ToArray();
    }

    /// <summary>
    /// Builds the departure packet (op 12) for a leave, expel, or disband (ports the
    /// <c>DISBAND/EXPEL/LEAVE</c> branch of <c>ResCWvsContext.PartyResult</c>). Disband carries just
    /// the leader id twice; leave/expel carry the expel flag, the departing name, and the refreshed
    /// member block (with the door "leaving" shape only for a voluntary leave).
    /// </summary>
    public byte[] PartyDepart(int partyId, int targetId, string targetName, PartyDepart kind, IReadOnlyList<PartyMemberView> slots, int leaderId, int forChannel)
    {
        PacketWriter w = NewPacket(ServerOpcode.PartyResult);
        w.WriteByte(PartyDepartOp);
        w.WriteInt(partyId);
        w.WriteInt(targetId);
        w.WriteBool(kind != Cronus.Server.Game.PartyDepart.Disband); // 0 = disband, 1 = a member left

        if (kind == Cronus.Server.Game.PartyDepart.Disband)
        {
            w.WriteInt(targetId);
        }
        else
        {
            w.WriteBool(kind == Cronus.Server.Game.PartyDepart.Expel); // 1 = expelled, 0 = voluntary
            w.WriteString(targetName);
            WritePartyStatus(w, slots, leaderId, forChannel, leaving: kind == Cronus.Server.Game.PartyDepart.Leave);
        }

        return w.ToArray();
    }

    /// <summary>
    /// Builds <c>PartyRes_ChangePartyBoss_Done</c> (op 31): the new leader's id and whether the
    /// change was caused by a disconnect.
    /// </summary>
    public byte[] PartyChangeLeader(int newLeaderId, bool byDisconnect)
    {
        PacketWriter w = NewPacket(ServerOpcode.PartyResult);
        w.WriteByte(PartyChangeLeaderOp);
        w.WriteInt(newLeaderId);
        w.WriteBool(byDisconnect);
        return w.ToArray();
    }

    /// <summary>
    /// Writes the 6-slot party member block (ports <c>ResCWvsContext.addPartyStatus</c>): ids,
    /// 13-byte names, jobs, levels, wire channels, the leader id, per-member map ids, then the
    /// per-member town-door block. <paramref name="slots"/> must already be padded to 6.
    /// </summary>
    private static void WritePartyStatus(PacketWriter w, IReadOnlyList<PartyMemberView> slots, int leaderId, int forChannel, bool leaving)
    {
        foreach (PartyMemberView m in slots)
        {
            w.WriteInt(m.Id);
        }

        foreach (PartyMemberView m in slots)
        {
            w.WriteFixedString(m.Name ?? string.Empty, 13);
        }

        foreach (PartyMemberView m in slots)
        {
            w.WriteInt(m.Job);
        }

        foreach (PartyMemberView m in slots)
        {
            w.WriteInt(m.Level);
        }

        foreach (PartyMemberView m in slots)
        {
            w.WriteInt(m.Online ? m.Channel - 1 : -2); // wire channel is 0-based; -2 = offline
        }

        w.WriteInt(leaderId);

        foreach (PartyMemberView m in slots)
        {
            w.WriteInt(m.Channel == forChannel ? m.MapId : 0);
        }

        foreach (PartyMemberView m in slots)
        {
            if (m.Channel == forChannel && !leaving)
            {
                w.WriteInt(PartyMemberView.NoDoor);  // door town
                w.WriteInt(PartyMemberView.NoDoor);  // door target
                w.WriteInt(0);                       // door skill
                w.WriteInt(0);                       // door x
                w.WriteInt(0);                       // door y
            }
            else
            {
                w.WriteInt(leaving ? PartyMemberView.NoDoor : 0);
                w.WriteLong(leaving ? PartyMemberView.NoDoor : 0);
                w.WriteLong(leaving ? -1 : 0);
            }
        }
    }

    /// <summary>
    /// Builds <c>LP_UserHP</c> so a party member sees another member's health bar (ports
    /// <c>ResCUserRemote.UserHP</c>): the member's character id, current HP, and max HP.
    /// </summary>
    public byte[] UserHP(int characterId, int currentHp, int maxHp)
    {
        PacketWriter w = NewPacket(ServerOpcode.UserHP);
        w.WriteInt(characterId);
        w.WriteInt(currentHp);
        w.WriteInt(maxHp);
        return w.ToArray();
    }

    // LP_GivePopularityResult ops (ports OpsGivePopularity).
    private const byte FameSuccess = 0;
    private const byte FameNotify = 5;

    /// <summary>Op byte of <c>GivePopularityRes_InvalidCharacterID</c> (self / bad target).</summary>
    public const int FameErrInvalidTarget = 1;

    /// <summary>Op byte of <c>GivePopularityRes_LevelLow</c> (giver below level 15).</summary>
    public const int FameErrLevelLow = 2;

    /// <summary>Op byte of <c>GivePopularityRes_AlreadyDoneToday</c> (already famed someone today).</summary>
    public const int FameErrAlreadyToday = 3;

    /// <summary>
    /// Builds a bare <c>LP_GivePopularityResult</c> (just the op byte) for the error/limit codes.
    /// </summary>
    public byte[] GivePopularityError(int op)
    {
        PacketWriter w = NewPacket(ServerOpcode.GivePopularityResult);
        w.WriteByte((byte)op);
        return w.ToArray();
    }

    /// <summary>
    /// Builds the giver-side <c>GivePopularityRes_Success</c>: the target's name, the direction
    /// (1 = up), and the target's new fame (ports <c>ResCWvsContext.GivePopularityResult</c>).
    /// </summary>
    public byte[] GivePopularitySuccess(string targetName, bool isUp, int targetFame)
    {
        PacketWriter w = NewPacket(ServerOpcode.GivePopularityResult);
        w.WriteByte(FameSuccess);
        w.WriteString(targetName);
        w.WriteBool(isUp);
        w.WriteInt(targetFame);
        return w.ToArray();
    }

    /// <summary>
    /// Builds the target-side <c>GivePopularityRes_Notify</c>: who famed them and the direction.
    /// </summary>
    public byte[] GivePopularityNotify(string giverName, bool isUp)
    {
        PacketWriter w = NewPacket(ServerOpcode.GivePopularityResult);
        w.WriteByte(FameNotify);
        w.WriteString(giverName);
        w.WriteBool(isUp);
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
