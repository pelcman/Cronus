using System.Collections.Concurrent;

namespace Cronus.Data;

/// <summary>
/// One of a mob's attacks (<c>attack{n}/info</c>), the server-relevant part (ports
/// <c>MobAttackInfo</c> as <c>MobWz.getMobAttackInfo</c> reads it): a deadly attack leaves the
/// victim at 1 HP/MP, <c>mpBurn</c> drains MP instead of HP, <c>disease</c>/<c>level</c> name a mob
/// skill (a player debuff — parsed, not yet applied: see <c>GameConstants.PlayerDiseasesEnabled</c>),
/// and <c>conMP</c> is what the attack costs the mob.
/// </summary>
public sealed record MobAttackInfo(bool DeadlyAttack, int MpBurn, int DiseaseSkill, int DiseaseLevel, int MpCon)
{
    public static readonly MobAttackInfo None = new(false, 0, 0, 0, 0);
}

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

    /// <summary>The template this mob borrows its attacks/animations from (<c>info/link</c>), 0 = none.</summary>
    public int Link { get; init; }

    /// <summary>Attacks by attack index (0 = <c>attack1</c>), only the ones carrying something
    /// server-relevant. Resolved through <see cref="Link"/> by the provider.</summary>
    public IReadOnlyDictionary<int, MobAttackInfo> Attacks { get; init; } = new Dictionary<int, MobAttackInfo>();

    /// <summary>The server-relevant info of attack <paramref name="attackIdx"/> (0-based, as the
    /// client reports it in CP_UserHit), or <see cref="MobAttackInfo.None"/>.</summary>
    public MobAttackInfo AttackAt(int attackIdx)
        => Attacks.TryGetValue(attackIdx, out MobAttackInfo? a) ? a : MobAttackInfo.None;

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
            Link = info?.GetInt("link") ?? 0,
            Attacks = ParseAttacks(mobImg),
        };
    }

    /// <summary>Reads every <c>attack{n}/info</c> that carries a server-relevant flag (ports
    /// <c>MobWz.getMobAttackInfo</c>: deadlyAttack = key present; the rest default 0).</summary>
    private static IReadOnlyDictionary<int, MobAttackInfo> ParseAttacks(WzData mobImg)
    {
        var attacks = new Dictionary<int, MobAttackInfo>();
        foreach ((string name, WzData node) in mobImg.Children)
        {
            if (!name.StartsWith("attack", StringComparison.Ordinal)
                || !int.TryParse(name.AsSpan("attack".Length), out int number)
                || node.Child("info") is not { } info)
            {
                continue;
            }

            var a = new MobAttackInfo(
                DeadlyAttack: info.Child("deadlyAttack") is not null,
                MpBurn: info.GetInt("mpBurn"),
                DiseaseSkill: info.GetInt("disease"),
                DiseaseLevel: info.GetInt("level"),
                MpCon: info.GetInt("conMP"));
            if (a != MobAttackInfo.None)
            {
                attacks[number - 1] = a;
            }
        }

        return attacks;
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
    private readonly IWzStore _store;
    private readonly ConcurrentDictionary<int, MobData?> _cache = new();

    public WzMobProvider(string wzRoot) : this(new DirectoryWzStore(wzRoot))
    {
    }

    public WzMobProvider(IWzStore store) => _store = store;

    public MobData? GetMob(int templateId) => _cache.GetOrAdd(templateId, Load);

    private MobData? Load(int templateId)
    {
        string? xml = _store.ReadText(MobImageRel(templateId));
        if (xml is null)
        {
            return null;
        }

        MobData mob = MobData.FromWz(templateId, WzData.ParseText(xml));

        // A linked mob keeps its own stats but borrows the attack table of its link target
        // (ports getMobAttackInfo's `info/link` hop).
        if (mob.Link > 0 && mob.Link != templateId && GetMob(mob.Link) is { } linked && linked.Attacks.Count > 0)
        {
            mob = new MobData
            {
                TemplateId = mob.TemplateId,
                MaxHp = mob.MaxHp,
                MaxMp = mob.MaxMp,
                Exp = mob.Exp,
                Level = mob.Level,
                TagColor = mob.TagColor,
                TagBgColor = mob.TagBgColor,
                Skills = mob.Skills,
                Revives = mob.Revives,
                Link = mob.Link,
                Attacks = linked.Attacks,
            };
        }

        return mob;
    }

    /// <summary>Store-relative form of <see cref="MobImagePath"/>.</summary>
    public static string MobImageRel(int templateId) => $"Mob/{templateId:0000000}.img.xml";

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
