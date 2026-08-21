using System.Collections.Concurrent;

namespace Cronus.Data;

/// <summary>
/// A skill's per-level effect (from Skill wz). Only the fields a buff skill needs are kept: MP cost,
/// duration, the generic stat buffs, and the <c>x</c>/<c>y</c> values special buffs use (Magic Guard,
/// Booster, Hyper Body, …). Duration is already converted to milliseconds (wz <c>time</c> ×1000).
/// </summary>
public sealed record SkillEffect
{
    public int MpCon { get; init; }

    /// <summary>Buff duration in ms (wz <c>time</c> seconds ×1000); 0 = not a timed buff.</summary>
    public int DurationMs { get; init; }

    public int Pad { get; init; }
    public int Pdd { get; init; }
    public int Mad { get; init; }
    public int Mdd { get; init; }
    public int Acc { get; init; }
    public int Eva { get; init; }
    public int Speed { get; init; }
    public int Jump { get; init; }

    /// <summary>The <c>x</c> value special buff skills use for their signature stat.</summary>
    public int X { get; init; }

    /// <summary>The <c>y</c> value some special buffs use (e.g. Hyper Body's MaxMP).</summary>
    public int Y { get; init; }

    /// <summary>
    /// True when the level carries an affect box (wz <c>lt</c>/<c>rb</c>) — the marker for a
    /// party-wide buff (Haste, Rage, Hyper Body, Bless, …), matching the reference's
    /// <c>isPartyBuff</c>.
    /// </summary>
    public bool HasPartyArea { get; init; }
}

/// <summary>
/// A mob skill's per-level data (Skill wz <c>MobSkill.img/{skillId}/level/{level}</c>): the HP%%
/// threshold below which the mob casts, the cooldown, the MP cost, the skill's <c>x</c>/<c>y</c>
/// magnitudes, and the summon list for skill 200.
/// </summary>
public sealed record MobSkillData
{
    public int X { get; init; }

    public int Y { get; init; }

    /// <summary>The mob casts only while its HP%% is at or below this (wz <c>hp</c>; 100 = always).</summary>
    public int HpThresholdPercent { get; init; } = 100;

    public int MpCon { get; init; }

    /// <summary>Cooldown between casts in ms (wz <c>interval</c> seconds ×1000).</summary>
    public int IntervalMs { get; init; }

    /// <summary>Summon cap on the field (wz <c>limit</c>, skill 200).</summary>
    public int Limit { get; init; }

    /// <summary>Mob template ids to summon (skill 200's numeric children).</summary>
    public IReadOnlyList<int> Summons { get; init; } = Array.Empty<int>();
}

/// <summary>Provides skill data (max level, and per-level buff effects) from Skill wz.</summary>
public interface ISkillProvider
{
    /// <summary>Max level for <paramref name="skillId"/>, or 0 when unknown (no data / caller decides).</summary>
    int GetMaxLevel(int skillId);

    /// <summary>The effect of <paramref name="skillId"/> at <paramref name="level"/>, or null if unknown.</summary>
    SkillEffect? GetSkillEffect(int skillId, int level);

    /// <summary>A mob skill's data at a level (<c>MobSkill.img</c>), or null if unknown.</summary>
    MobSkillData? GetMobSkill(int skillId, int level);

    /// <summary>All skill ids in a job's skill book (<c>Skill/{jobId:000}.img</c>); empty if none.</summary>
    IReadOnlyList<int> GetSkillIds(int jobId);
}

/// <summary>
/// Loads skill max-levels from a wz_xml tree: <c>Skill/{skillId/10000:000}.img.xml</c> →
/// <c>skill/{skillId}/level</c> (max level = number of level entries). Results are cached per
/// skill id; unknown/missing skills return 0.
/// </summary>
public sealed class WzSkillProvider : ISkillProvider
{
    private readonly string _wzRoot;
    private readonly ConcurrentDictionary<int, int> _cache = new();

    public WzSkillProvider(string wzRoot)
    {
        _wzRoot = wzRoot;
        _mobSkillImg = new Lazy<WzData?>(() =>
        {
            string path = Path.Combine(_wzRoot, "Skill", "MobSkill.img.xml");
            return File.Exists(path) ? WzData.ParseFile(path) : null;
        });
    }

    public int GetMaxLevel(int skillId) => _cache.GetOrAdd(skillId, Load);

    private int Load(int skillId)
    {
        string path = SkillImagePath(_wzRoot, skillId);
        return File.Exists(path) ? MaxLevelFromWz(WzData.ParseFile(path), skillId) : 0;
    }

    private readonly ConcurrentDictionary<long, SkillEffect?> _effectCache = new();

    public SkillEffect? GetSkillEffect(int skillId, int level)
        => _effectCache.GetOrAdd(((long)skillId << 8) | (uint)(byte)level, _ => LoadEffect(skillId, level));

    private SkillEffect? LoadEffect(int skillId, int level)
    {
        string path = SkillImagePath(_wzRoot, skillId);
        if (level < 1 || !File.Exists(path))
        {
            return null;
        }

        WzData? skillDir = WzData.ParseFile(path).Child("skill");
        if (skillDir is null)
        {
            return null;
        }

        foreach (WzData skill in skillDir.Children.Values)
        {
            if (!int.TryParse(skill.Name, out int id) || id != skillId)
            {
                continue;
            }

            WzData? lvl = skill.Child("level")?.Child(level.ToString());
            if (lvl is null)
            {
                return null;
            }

            return new SkillEffect
            {
                MpCon = lvl.GetInt("mpCon"),
                DurationMs = lvl.GetInt("time") * 1000, // skill time is seconds
                Pad = lvl.GetInt("pad"),
                Pdd = lvl.GetInt("pdd"),
                Mad = lvl.GetInt("mad"),
                Mdd = lvl.GetInt("mdd"),
                Acc = lvl.GetInt("acc"),
                Eva = lvl.GetInt("eva"),
                Speed = lvl.GetInt("speed"),
                Jump = lvl.GetInt("jump"),
                X = lvl.GetInt("x"),
                Y = lvl.GetInt("y"),
                HasPartyArea = lvl.Child("lt") is not null,
            };
        }

        return null;
    }

    private readonly ConcurrentDictionary<int, IReadOnlyList<int>> _jobSkillCache = new();

    public IReadOnlyList<int> GetSkillIds(int jobId) => _jobSkillCache.GetOrAdd(jobId, LoadSkillIds);

    private IReadOnlyList<int> LoadSkillIds(int jobId)
    {
        string path = Path.Combine(_wzRoot, "Skill", $"{jobId:000}.img.xml");
        if (!File.Exists(path))
        {
            return Array.Empty<int>();
        }

        WzData? skillDir = WzData.ParseFile(path).Child("skill");
        if (skillDir is null)
        {
            return Array.Empty<int>();
        }

        var ids = new List<int>();
        foreach (WzData skill in skillDir.Children.Values)
        {
            if (int.TryParse(skill.Name, out int id))
            {
                ids.Add(id);
            }
        }

        ids.Sort();
        return ids;
    }

    private readonly ConcurrentDictionary<long, MobSkillData?> _mobSkillCache = new();

    public MobSkillData? GetMobSkill(int skillId, int level)
        => _mobSkillCache.GetOrAdd(((long)skillId << 8) | (uint)(byte)level, _ => LoadMobSkill(skillId, level));

    private MobSkillData? LoadMobSkill(int skillId, int level)
    {
        if (level < 1)
        {
            return null;
        }

        WzData? lvl = _mobSkillImg.Value?.Child(skillId.ToString())?.Child("level")?.Child(level.ToString());
        if (lvl is null)
        {
            return null;
        }

        var summons = new List<int>();
        foreach (WzData child in lvl.Children.Values)
        {
            if (int.TryParse(child.Name, out _) && child.AsInt() > 0)
            {
                summons.Add(child.AsInt());
            }
        }

        return new MobSkillData
        {
            X = lvl.GetInt("x"),
            Y = lvl.GetInt("y"),
            HpThresholdPercent = lvl.GetInt("hp", 100),
            MpCon = lvl.GetInt("mpCon"),
            IntervalMs = lvl.GetInt("interval") * 1000,
            Limit = lvl.GetInt("limit"),
            Summons = summons,
        };
    }

    /// <summary>MobSkill.img parsed once, lazily (it is one shared document).</summary>
    private readonly Lazy<WzData?> _mobSkillImg;

    /// <summary>
    /// Reads a skill's max level from a parsed Skill <c>.img</c> (<c>skill/{id}/level</c> child
    /// count). Skill node names carry the id (sometimes zero-padded), so match by numeric value.
    /// Returns 0 when the skill isn't in this document.
    /// </summary>
    public static int MaxLevelFromWz(WzData skillImg, int skillId)
    {
        WzData? skillDir = skillImg.Child("skill");
        if (skillDir is null)
        {
            return 0;
        }

        foreach (WzData skill in skillDir.Children.Values)
        {
            if (int.TryParse(skill.Name, out int id) && id == skillId)
            {
                return skill.Child("level")?.Children.Count ?? 0;
            }
        }

        return 0;
    }

    /// <summary>The Skill <c>.img.xml</c> file for a skill: named by the job (skillId / 10000).</summary>
    public static string SkillImagePath(string wzRoot, int skillId)
        => Path.Combine(wzRoot, "Skill", $"{skillId / 10000:000}.img.xml");
}

/// <summary>An <see cref="ISkillProvider"/> with no data — every skill is "unknown" (max 0).</summary>
public sealed class NullSkillProvider : ISkillProvider
{
    public static readonly NullSkillProvider Instance = new();

    public int GetMaxLevel(int skillId) => 0;

    public SkillEffect? GetSkillEffect(int skillId, int level) => null;

    public MobSkillData? GetMobSkill(int skillId, int level) => null;

    public IReadOnlyList<int> GetSkillIds(int jobId) => Array.Empty<int>();
}

/// <summary>An in-memory skill provider for tests / seeded content.</summary>
public sealed class InMemorySkillProvider : ISkillProvider
{
    private readonly IReadOnlyDictionary<int, int> _maxLevels;
    private readonly IReadOnlyDictionary<(int SkillId, int Level), SkillEffect> _effects;
    private readonly IReadOnlyDictionary<(int SkillId, int Level), MobSkillData> _mobSkills;

    public InMemorySkillProvider(
        IReadOnlyDictionary<int, int>? maxLevels = null,
        IReadOnlyDictionary<(int, int), SkillEffect>? effects = null,
        IReadOnlyDictionary<(int, int), MobSkillData>? mobSkills = null)
    {
        _maxLevels = maxLevels ?? new Dictionary<int, int>();
        _effects = effects ?? new Dictionary<(int, int), SkillEffect>();
        _mobSkills = mobSkills ?? new Dictionary<(int, int), MobSkillData>();
    }

    public int GetMaxLevel(int skillId) => _maxLevels.TryGetValue(skillId, out int m) ? m : 0;

    public SkillEffect? GetSkillEffect(int skillId, int level)
        => _effects.TryGetValue((skillId, level), out SkillEffect? e) ? e : null;

    public MobSkillData? GetMobSkill(int skillId, int level)
        => _mobSkills.TryGetValue((skillId, level), out MobSkillData? m) ? m : null;

    public IReadOnlyList<int> GetSkillIds(int jobId)
        => _maxLevels.Keys.Where(id => id / 10000 == jobId).OrderBy(id => id).ToList();
}
