using System.Collections.Concurrent;

namespace Cronus.Data;

/// <summary>A quest requirement/reward item row (<c>id</c>/<c>count</c>; negative count = taken
/// away). <see cref="Prop"/> mirrors the wz <c>prop</c> when present (−1 = player-selectable,
/// &gt;0 = weighted random) — rows with a prop are the choose/lottery rewards. <see cref="Gender"/>
/// and <see cref="Job"/>/<see cref="JobEx"/> mirror the per-row filters (ports
/// <c>MapleQuestAction.canGetItem</c>): gender 2 = both, job is a bitmask of job families.</summary>
public sealed record QuestItemEntry(int ItemId, int Count, int? Prop = null, int? Gender = null, long? Job = null, long? JobEx = null)
{
    /// <summary>The base job each bit of the wz <c>job</c> bitmask selects (5-byte encoding,
    /// pre-BB range; ports <c>MapleQuestAction.getJobBy5ByteEncoding</c>).</summary>
    private static readonly (long Bit, int BaseJob)[] JobBits =
    {
        (0x1, 0), (0x2, 100), (0x4, 200), (0x8, 300), (0x10, 400), (0x20, 500),
        (0x400, 1000), (0x800, 1100), (0x1000, 1200), (0x2000, 1300), (0x4000, 1400), (0x8000, 1500),
        (0x20000, 2200), (0x100000, 2000), (0x100000, 2001),
    };

    /// <summary>Whether this row applies to a player (gender first, then the job-family bitmask
    /// with <see cref="JobEx"/> as the fallback — the reference's <c>canGetItem</c> order).</summary>
    public bool AppliesTo(int job, int gender)
    {
        if (Gender is { } g && g != 2 && g != gender)
        {
            return false;
        }

        if (Job is not { } flags)
        {
            return true;
        }

        return MatchesJobFamily(flags, job) || (JobEx is { } ex && MatchesJobFamily(ex, job));
    }

    private static bool MatchesJobFamily(long flags, int job)
    {
        foreach ((long bit, int baseJob) in JobBits)
        {
            if ((flags & bit) != 0 && baseJob / 100 == job / 100)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>A quest mob-kill requirement (<c>id</c>/<c>count</c>).</summary>
public sealed record QuestMobEntry(int MobId, int Count);

/// <summary>A prerequisite quest (<c>id</c> must be at <c>State</c>: 1 = started, 2 = completed).</summary>
public sealed record QuestPrereq(int QuestId, int State);

/// <summary>One side of a quest's <c>Check.img</c> node (0 = start, 1 = complete).</summary>
public sealed class QuestCheck
{
    /// <summary>The NPC this side talks to (start or turn-in), 0 when absent.</summary>
    public int Npc { get; init; }

    /// <summary>Minimum level to start (start side), 0 when absent.</summary>
    public int LevelMin { get; init; }

    /// <summary>Maximum level to start (<c>lvmax</c>, start side), 0 = no cap.</summary>
    public int LevelMax { get; init; }

    /// <summary>Repeat interval in minutes (<c>interval</c>, start side): the quest can be done
    /// again once this long has passed since its last completion. 0 = not repeatable.</summary>
    public int IntervalMinutes { get; init; }

    /// <summary>Mobs to kill (complete side), in wz order — the order of the progress string.</summary>
    public IReadOnlyList<QuestMobEntry> Mobs { get; init; } = Array.Empty<QuestMobEntry>();

    /// <summary>Items required (complete side).</summary>
    public IReadOnlyList<QuestItemEntry> Items { get; init; } = Array.Empty<QuestItemEntry>();

    /// <summary>Script name (wz <c>startscript</c>/<c>endscript</c>) when this side is script-driven.</summary>
    public string Script { get; init; } = string.Empty;

    /// <summary>Prerequisite quests (start side).</summary>
    public IReadOnlyList<QuestPrereq> Quests { get; init; } = Array.Empty<QuestPrereq>();

    /// <summary>Eligible job ids (start side); empty = any job.</summary>
    public IReadOnlyList<int> Jobs { get; init; } = Array.Empty<int>();
}

/// <summary>A skill granted by a quest act (<c>id</c>/<c>skillLevel</c>/<c>masterLevel</c>, with an
/// optional job filter — grant only when the player's job is listed).</summary>
public sealed record QuestSkillEntry(int SkillId, int SkillLevel, int MasterLevel, IReadOnlyList<int> Jobs);

/// <summary>One side of a quest's <c>Act.img</c> node (0 = on start, 1 = on completion).</summary>
public sealed class QuestAct
{
    public int Exp { get; init; }

    public int Money { get; init; }

    /// <summary>Fame ("pop") granted.</summary>
    public int Fame { get; init; }

    /// <summary>Items given (positive count) or taken (negative count).</summary>
    public IReadOnlyList<QuestItemEntry> Items { get; init; } = Array.Empty<QuestItemEntry>();

    /// <summary>The quest the client auto-opens next (<c>nextQuest</c>), 0 = none. This is how the
    /// tutorial/beginner chains flow from one quest straight into the next.</summary>
    public int NextQuest { get; init; }

    /// <summary>Skills granted (or reset to level 0) by this act.</summary>
    public IReadOnlyList<QuestSkillEntry> Skills { get; init; } = Array.Empty<QuestSkillEntry>();

    /// <summary>Other quests whose state this act sets (<c>id</c>/<c>state</c>: 1 started, 2 completed).</summary>
    public IReadOnlyList<QuestPrereq> QuestStates { get; init; } = Array.Empty<QuestPrereq>();

    /// <summary>SP grants (<c>sp</c> rows: <c>sp_value</c> + optional job filter).</summary>
    public IReadOnlyList<QuestSpEntry> SpGrants { get; init; } = Array.Empty<QuestSpEntry>();

    /// <summary>Item whose wz buff effect is applied by this act (<c>buffItemID</c>), 0 = none.</summary>
    public int BuffItemId { get; init; }
}

/// <summary>An SP grant in a quest act; <see cref="Jobs"/> empty = any job, otherwise granted when
/// the player's job is at or past one of the listed jobs (the reference picks the matching book).</summary>
public sealed record QuestSpEntry(int SpValue, IReadOnlyList<int> Jobs);

/// <summary>A quest definition: start/complete requirements and acts from Quest wz.</summary>
public sealed class QuestData
{
    public required int QuestId { get; init; }

    public QuestCheck? StartCheck { get; init; }

    public QuestCheck? EndCheck { get; init; }

    public QuestAct? StartAct { get; init; }

    public QuestAct? EndAct { get; init; }
}

/// <summary>Provides quest definitions by id.</summary>
public interface IQuestProvider
{
    QuestData? GetQuest(int questId);
}

/// <summary>
/// Loads quests from the wz_xml tree's <c>Quest/Check.img.xml</c> + <c>Quest/Act.img.xml</c>
/// (each one large file holding every quest, parsed lazily once and then indexed per quest).
/// </summary>
public sealed class WzQuestProvider : IQuestProvider
{
    private readonly Lazy<WzData?> _check;
    private readonly Lazy<WzData?> _act;
    private readonly ConcurrentDictionary<int, QuestData?> _cache = new();

    public WzQuestProvider(string wzRoot) : this(new DirectoryWzStore(wzRoot))
    {
    }

    public WzQuestProvider(IWzStore store)
    {
        _check = new Lazy<WzData?>(() => LoadImg(store, "Check"));
        _act = new Lazy<WzData?>(() => LoadImg(store, "Act"));
    }

    private static WzData? LoadImg(IWzStore store, string name)
        => store.ReadText($"Quest/{name}.img.xml") is { } xml ? WzData.ParseText(xml) : null;

    public QuestData? GetQuest(int questId) => _cache.GetOrAdd(questId, Load);

    private QuestData? Load(int questId)
    {
        WzData? check = _check.Value?.Child(questId.ToString());
        WzData? act = _act.Value?.Child(questId.ToString());
        if (check is null && act is null)
        {
            return null;
        }

        return new QuestData
        {
            QuestId = questId,
            StartCheck = ParseCheck(check?.Child("0")),
            EndCheck = ParseCheck(check?.Child("1")),
            StartAct = ParseAct(act?.Child("0")),
            EndAct = ParseAct(act?.Child("1")),
        };
    }

    private static QuestCheck? ParseCheck(WzData? node)
    {
        if (node is null)
        {
            return null;
        }

        return new QuestCheck
        {
            Npc = node.GetInt("npc"),
            LevelMin = node.GetInt("lvmin"),
            LevelMax = node.GetInt("lvmax"),
            IntervalMinutes = node.GetInt("interval"),
            Mobs = ParseList(node.Child("mob"), row => new QuestMobEntry(row.GetInt("id"), row.GetInt("count"))),
            Items = ParseItems(node.Child("item")),
            Script = node.GetString("startscript", node.GetString("endscript", string.Empty)),
            Quests = ParseList(node.Child("quest"), row => new QuestPrereq(row.GetInt("id"), row.GetInt("state", 2))),
            Jobs = ParseJobs(node.Child("job")),
        };
    }

    private static QuestAct? ParseAct(WzData? node)
    {
        if (node is null)
        {
            return null;
        }

        return new QuestAct
        {
            Exp = node.GetInt("exp"),
            Money = node.GetInt("money"),
            Fame = node.GetInt("pop"),
            Items = ParseItems(node.Child("item")),
            NextQuest = node.GetInt("nextQuest"),
            Skills = ParseList(node.Child("skill"), row => new QuestSkillEntry(
                row.GetInt("id"),
                row.GetInt("skillLevel"),
                row.GetInt("masterLevel"),
                ParseJobs(row.Child("job")))),
            QuestStates = ParseList(node.Child("quest"), row => new QuestPrereq(row.GetInt("id"), row.GetInt("state", 2))),
            SpGrants = ParseList(node.Child("sp"), row => new QuestSpEntry(
                row.GetInt("sp_value"),
                ParseJobs(row.Child("job")))),
            BuffItemId = node.GetInt("buffItemID"),
        };
    }

    private static IReadOnlyList<QuestItemEntry> ParseItems(WzData? list)
        => ParseList(list, row => new QuestItemEntry(
            row.GetInt("id"),
            row.GetInt("count"),
            row.Child("prop") is null ? null : row.GetInt("prop"),
            row.Child("gender") is null ? null : row.GetInt("gender"),
            row.Child("job") is null ? null : row.GetInt("job"),
            row.Child("jobEx") is null ? null : row.GetInt("jobEx")));

    /// <summary>Reads a job-id list (int leaves named "0","1",…) in numeric order.</summary>
    private static IReadOnlyList<int> ParseJobs(WzData? list)
    {
        if (list is null || list.Children.Count == 0)
        {
            return Array.Empty<int>();
        }

        return list.Children.Values
            .Where(c => int.TryParse(c.Name, out _))
            .OrderBy(c => int.Parse(c.Name))
            .Select(c => c.AsInt())
            .ToList();
    }

    /// <summary>Reads a wz list node ("0","1",…) in numeric order.</summary>
    private static IReadOnlyList<T> ParseList<T>(WzData? list, Func<WzData, T> map)
    {
        if (list is null || list.Children.Count == 0)
        {
            return Array.Empty<T>();
        }

        return list.Children.Values
            .Where(c => int.TryParse(c.Name, out _))
            .OrderBy(c => int.Parse(c.Name))
            .Select(map)
            .ToList();
    }
}

/// <summary>An in-memory quest provider for tests / seeded content.</summary>
public sealed class InMemoryQuestProvider : IQuestProvider
{
    private readonly Dictionary<int, QuestData> _quests;

    public InMemoryQuestProvider(IEnumerable<QuestData> quests)
        => _quests = quests.ToDictionary(q => q.QuestId);

    public QuestData? GetQuest(int questId) => _quests.TryGetValue(questId, out QuestData? q) ? q : null;
}
