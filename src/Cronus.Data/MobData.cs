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
        };
    }
}

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
