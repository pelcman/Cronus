using System.Collections.Concurrent;

namespace Cronus.Data;

/// <summary>A quest requirement/reward item row (<c>id</c>/<c>count</c>; negative count = taken
/// away). <see cref="Prop"/> mirrors the wz <c>prop</c> when present (−1 = player-selectable,
/// &gt;0 = weighted random) — rows with a prop are the choose/lottery rewards.</summary>
public sealed record QuestItemEntry(int ItemId, int Count, int? Prop = null);

/// <summary>A quest mob-kill requirement (<c>id</c>/<c>count</c>).</summary>
public sealed record QuestMobEntry(int MobId, int Count);

/// <summary>One side of a quest's <c>Check.img</c> node (0 = start, 1 = complete).</summary>
public sealed class QuestCheck
{
    /// <summary>The NPC this side talks to (start or turn-in), 0 when absent.</summary>
    public int Npc { get; init; }

    /// <summary>Minimum level to start (start side), 0 when absent.</summary>
    public int LevelMin { get; init; }

    /// <summary>Mobs to kill (complete side), in wz order — the order of the progress string.</summary>
    public IReadOnlyList<QuestMobEntry> Mobs { get; init; } = Array.Empty<QuestMobEntry>();

    /// <summary>Items required (complete side).</summary>
    public IReadOnlyList<QuestItemEntry> Items { get; init; } = Array.Empty<QuestItemEntry>();
}

/// <summary>One side of a quest's <c>Act.img</c> node (0 = on start, 1 = on completion).</summary>
public sealed class QuestAct
{
    public int Exp { get; init; }

    public int Money { get; init; }

    /// <summary>Fame ("pop") granted.</summary>
    public int Fame { get; init; }

    /// <summary>Items given (positive count) or taken (negative count).</summary>
    public IReadOnlyList<QuestItemEntry> Items { get; init; } = Array.Empty<QuestItemEntry>();
}

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

    public WzQuestProvider(string wzRoot)
    {
        _check = new Lazy<WzData?>(() => LoadImg(wzRoot, "Check"));
        _act = new Lazy<WzData?>(() => LoadImg(wzRoot, "Act"));
    }

    private static WzData? LoadImg(string wzRoot, string name)
    {
        string path = Path.Combine(wzRoot, "Quest", $"{name}.img.xml");
        return File.Exists(path) ? WzData.ParseFile(path) : null;
    }

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
            Mobs = ParseList(node.Child("mob"), row => new QuestMobEntry(row.GetInt("id"), row.GetInt("count"))),
            Items = ParseItems(node.Child("item")),
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
        };
    }

    private static IReadOnlyList<QuestItemEntry> ParseItems(WzData? list)
        => ParseList(list, row => new QuestItemEntry(
            row.GetInt("id"),
            row.GetInt("count"),
            row.Child("prop") is null ? null : row.GetInt("prop")));

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
