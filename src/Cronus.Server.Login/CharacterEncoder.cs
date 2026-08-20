using Cronus.Domain;
using Cronus.Network.Packets;

namespace Cronus.Server.Login;

/// <summary>
/// Serializes a character for the JMS v186 login stage. Ports the JMS branch of
/// <c>DataGW_CharacterStat.Encode</c> and <c>DataAvatarLook.Encode</c> (pre-Big-Bang: v186 &lt;
/// 187, so HP/MP are 16-bit and the pre-BB tails apply). Equipment is not rendered yet.
/// </summary>
public static class CharacterEncoder
{
    /// <summary>Fixed character-name field width on the wire (JMS).</summary>
    private const int NameLength = 13;

    /// <summary>Writes GW_CharacterStat for <paramref name="c"/> into <paramref name="w"/>.</summary>
    public static void WriteStat(PacketWriter w, Character c)
    {
        w.WriteInt(c.Id);                      // dwCharacterID
        w.WriteFixedString(c.Name, NameLength);
        w.WriteByte(c.Gender);
        w.WriteByte(c.SkinColor);
        w.WriteInt(c.Face);
        w.WriteInt(c.Hair);
        w.WriteZero(24);                       // pet ids / reserved (JMS pre-BB)

        w.WriteByte(c.Level);
        w.WriteShort(c.Job);

        w.WriteShort(c.Str);
        w.WriteShort(c.Dex);
        w.WriteShort(c.Int);
        w.WriteShort(c.Luk);

        // Pre-Big-Bang: HP/MP are 16-bit.
        w.WriteShort(c.Hp);
        w.WriteShort(c.MaxHp);
        w.WriteShort(c.Mp);
        w.WriteShort(c.MaxMp);

        w.WriteShort(c.Ap);
        w.WriteShort(c.Sp);                    // basic (non-extend-SP) job

        w.WriteInt(c.Exp);                     // nEXP
        w.WriteShort(c.Fame);                  // nPOP
        w.WriteInt(c.GashaExp);                // nTempEXP (JMS >= 146)

        w.WriteInt(c.MapId);                   // dwPosMap
        w.WriteByte(c.Portal);                 // nPortal
        w.WriteShort(c.SubCategory);           // (JMS >= 180)

        // JMS pre-BB tail.
        w.WriteLong(0);
        w.WriteInt(0);
        w.WriteInt(0);
        w.WriteInt(0);                         // (JMS >= 180)
    }

    /// <summary>Writes AvatarLook for <paramref name="c"/> (no visible equipment).</summary>
    public static void WriteAvatarLook(PacketWriter w, Character c)
    {
        w.WriteByte(c.Gender);
        w.WriteByte(c.SkinColor);
        w.WriteInt(c.Face);
        w.WriteByte(0);                        // ignored byte
        w.WriteInt(c.Hair);

        // Visible equipment (none) then terminator.
        w.WriteByte(0xFF);
        // Masked equipment (none) then terminator.
        w.WriteByte(0xFF);

        w.WriteInt(0);                         // weapon sticker id
        w.WriteInt(0);                         // pet 1
        w.WriteLong(0);                        // pet 2 & 3 (JMS >= 146)
    }

    /// <summary>
    /// Writes a full character entry for <c>LP_SelectWorldResult</c>: stat, look, the JMS-&gt;=180
    /// family byte, and the ranking block.
    /// </summary>
    public static void WriteSelectWorldEntry(PacketWriter w, Character c)
    {
        WriteStat(w, c);
        WriteAvatarLook(w, c);
        w.WriteByte(0);      // family (JMS >= 180)
        WriteRanking(w, c);
    }

    /// <summary>
    /// Writes a full character entry for <c>LP_ViewAllCharResult</c>: stat, look, ranking
    /// (no family byte).
    /// </summary>
    public static void WriteViewAllEntry(PacketWriter w, Character c)
    {
        WriteStat(w, c);
        WriteAvatarLook(w, c);
        WriteRanking(w, c);
    }

    private static void WriteRanking(PacketWriter w, Character c)
    {
        w.WriteByte(1);              // ranking present
        w.WriteInt(c.Rank);
        w.WriteInt(c.RankMove);
        w.WriteInt(c.JobRank);
        w.WriteInt(c.JobRankMove);
    }
}
