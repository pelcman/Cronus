namespace Cronus.Domain;

/// <summary>
/// A player character. Fields cover what the JMS v186 login-stage encoders
/// (GW_CharacterStat / AvatarLook) need to render a character on the selection screen.
/// Equipment is not persisted yet (characters render without visible equips until creation
/// seeds starter items).
/// </summary>
public sealed class Character
{
    public int Id { get; set; }

    public int AccountId { get; set; }

    public int WorldId { get; set; }

    public required string Name { get; set; }

    // Appearance
    public byte Gender { get; set; }
    public byte SkinColor { get; set; }
    public int Face { get; set; }
    public int Hair { get; set; }

    // Progression
    public byte Level { get; set; } = 1;
    public short Job { get; set; }

    // Base stats
    public short Str { get; set; } = 4;
    public short Dex { get; set; } = 4;
    public short Int { get; set; } = 4;
    public short Luk { get; set; } = 4;

    // Derived stats (pre-Big-Bang = 16-bit on the wire for v186)
    public short Hp { get; set; } = 50;
    public short MaxHp { get; set; } = 50;
    public short Mp { get; set; } = 5;
    public short MaxMp { get; set; } = 5;

    public short Ap { get; set; }
    public short Sp { get; set; }

    public int Exp { get; set; }
    public short Fame { get; set; }
    public int GashaExp { get; set; }

    // Location
    public int MapId { get; set; }
    public byte Portal { get; set; }
    public short SubCategory { get; set; }

    public int Meso { get; set; }

    // Ranking (shown on the character card)
    public int Rank { get; set; } = 1;
    public int RankMove { get; set; }
    public int JobRank { get; set; } = 1;
    public int JobRankMove { get; set; }

    /// <summary>Equipped items (negative positions); persisted via the items table.</summary>
    public List<InventoryItem> EquippedItems { get; set; } = new();

    /// <summary>Started quests: quest id → progress/custom data. In-memory (EF-ignored) for now.</summary>
    public Dictionary<int, string> StartedQuests { get; set; } = new();

    /// <summary>Completed quests: quest id → completion time (Windows FILETIME). EF-ignored for now.</summary>
    public Dictionary<int, long> CompletedQuests { get; set; } = new();

    /// <summary>Learned skills: skill id → level. In-memory (EF-ignored) for now.</summary>
    public Dictionary<int, int> Skills { get; set; } = new();
}
