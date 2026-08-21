namespace Cronus.Server.Game;

/// <summary>
/// Character stat change bits (ports <c>OpsChangeStat</c>) used by <c>LP_StatChanged</c>.
/// The encoder writes set stats in ascending bit order, matching
/// <c>DataGW_CharacterStat.EncodeChangeStat</c>.
/// </summary>
[Flags]
public enum StatFlag
{
    None = 0,
    Skin = 0x1,
    Face = 0x2,
    Hair = 0x4,
    Level = 0x10,
    Job = 0x20,
    Str = 0x40,
    Dex = 0x80,
    Int = 0x100,
    Luk = 0x200,
    Hp = 0x400,
    MaxHp = 0x800,
    Mp = 0x1000,
    MaxMp = 0x2000,
    Ap = 0x4000,
    Sp = 0x8000,
    Exp = 0x10000,
    Fame = 0x20000,
    Meso = 0x40000,
}
