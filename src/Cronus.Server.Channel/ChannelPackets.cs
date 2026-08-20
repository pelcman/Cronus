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
        const int spawnStance = 5;

        PacketWriter w = NewPacket(ServerOpcode.MobEnterField);
        w.WriteInt(mob.ObjectId);     // dwMobID (runtime oid)
        w.WriteByte(1);               // 1 = control normal
        w.WriteInt(mob.TemplateId);   // mob template

        // CMob::SetTemporaryStat — JMS v186 mask is 4 ints, all zero (no buffs).
        for (int i = 0; i < 4; i++)
        {
            w.WriteInt(0);
        }

        // CMob::Init
        w.WriteShort(mob.X);
        w.WriteShort(mob.Y);
        w.WriteByte(spawnStance);            // m_nMoveAction
        w.WriteShort((short)mob.Foothold);   // current foothold
        w.WriteShort((short)mob.Foothold);   // origin foothold
        w.WriteByte(unchecked((byte)-1));    // nAppearType = MOBAPPEAR_NORMAL
        w.WriteByte(unchecked((byte)-1));    // m_nTeamForMCarnival
        w.WriteInt(0);                       // nEffectItemID (JMS >= 146)
        w.WriteInt(0);                       // m_nPhase (JMS >= 165)
        return w.ToArray();
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
