using System.Collections.Concurrent;

namespace Cronus.Data;

/// <summary>Server-relevant stats for a monster template (from a Mob <c>.img</c>: <c>info/*</c>).</summary>
public sealed class MobData
{
    public required int TemplateId { get; init; }

    public int MaxHp { get; init; } = 100;

    public int MaxMp { get; init; }

    public int Exp { get; init; }

    public int Level { get; init; } = 1;

    /// <summary>
    /// Boss HP-gauge tag colour (<c>info/hpTagColor</c>); 0 for ordinary mobs. A non-zero value
    /// marks a boss whose HP bar shows at the bottom of the screen.
    /// </summary>
    public int TagColor { get; init; }

    /// <summary>Boss HP-gauge background colour (<c>info/hpTagBgcolor</c>); 0 for ordinary mobs.</summary>
    public int TagBgColor { get; init; }

    /// <summary>The mob's castable skills (<c>info/skill/{n}</c>); empty for plain mobs.</summary>
    public IReadOnlyList<MobSkillEntry> Skills { get; init; } = Array.Empty<MobSkillEntry>();

    /// <summary>Mobs spawned in place when this one dies (<c>info/revive</c> — boss phases).</summary>
    public IReadOnlyList<int> Revives { get; init; } = Array.Empty<int>();

    /// <summary>Parses a Mob <c>.img</c> WZ document's <c>info</c> subtree.</summary>
    public static MobData FromWz(int templateId, WzData mobImg)
    {
        WzData? info = mobImg.Child("info");
        return new MobData
        {
            TemplateId = templateId,
            MaxHp = info?.GetInt("maxHP", 100) ?? 100,
            MaxMp = info?.GetInt("maxMP") ?? 0,
            Exp = info?.GetInt("exp") ?? 0,
            Level = info?.GetInt("level", 1) ?? 1,
            TagColor = info?.GetInt("hpTagColor") ?? 0,
            TagBgColor = info?.GetInt("hpTagBgcolor") ?? 0,
            Skills = ParseSkills(info?.Child("skill")),
            Revives = ParseRevives(info?.Child("revive")),
        };
    }

    private static IReadOnlyList<int> ParseRevives(WzData? reviveDir)
    {
        if (reviveDir is null)
        {
            return Array.Empty<int>();
        }

        var revives = new List<int>();
        foreach (WzData entry in reviveDir.Children.Values)
        {
            if (entry.AsInt(0) > 0)
            {
                revives.Add(entry.AsInt(0));
            }
        }

        return revives;
    }

    private static IReadOnlyList<MobSkillEntry> ParseSkills(WzData? skillDir)
    {
        if (skillDir is null)
        {
            return Array.Empty<MobSkillEntry>();
        }

        var skills = new List<MobSkillEntry>();
        foreach (WzData entry in skillDir.Children.Values)
        {
            int skill = entry.GetInt("skill");
            if (skill > 0)
            {
                skills.Add(new MobSkillEntry(skill, Math.Max(1, entry.GetInt("level", 1))));
            }
        }

        return skills;
    }
}

/// <summary>One castable skill on a mob template (<c>info/skill/{n}</c>: skill + level).</summary>
public readonly record struct MobSkillEntry(int SkillId, int Level);

/// <summary>Provides <see cref="MobData"/> by template id.</summary>
public interface IMobProvider
{
    MobData? GetMob(int templateId);
}

/// <summary>
/// Loads mob data from a wz_xml tree: <c>Mob/{templateId:0000000}.img.xml</c> (cached).
/// Missing files return null (callers fall back to defaults).
/// </summary>
public sealed class WzMobProvider : IMobProvider
{
    private readonly string _wzRoot;
    private readonly ConcurrentDictionary<int, MobData?> _cache = new();

    public WzMobProvider(string wzRoot) => _wzRoot = wzRoot;

    public MobData? GetMob(int templateId) => _cache.GetOrAdd(templateId, Load);

    private MobData? Load(int templateId)
    {
        string path = MobImagePath(_wzRoot, templateId);
        if (!File.Exists(path))
        {
            return null;
        }

        return MobData.FromWz(templateId, WzData.ParseFile(path));
    }

    public static string MobImagePath(string wzRoot, int templateId)
        => Path.Combine(wzRoot, "Mob", $"{templateId:0000000}.img.xml");
}

/// <summary>An in-memory mob provider for tests / seeded content.</summary>
public sealed class InMemoryMobProvider : IMobProvider
{
    private readonly Dictionary<int, MobData> _mobs;

    public InMemoryMobProvider(IEnumerable<MobData> mobs)
        => _mobs = mobs.ToDictionary(m => m.TemplateId);

    public MobData? GetMob(int templateId) => _mobs.TryGetValue(templateId, out MobData? m) ? m : null;
}
