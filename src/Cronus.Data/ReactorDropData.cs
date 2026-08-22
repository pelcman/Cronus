using System.Text.RegularExpressions;

namespace Cronus.Data;

/// <summary>
/// One row of a reactor's drop table (ports <c>ReactorDropEntry</c> + the <c>reactordrops</c>
/// schema). <see cref="Chance"/> is a 1-in-N probability (the reference rolls
/// <c>Math.random() &lt; 1/chance</c>); <see cref="QuestId"/> &gt; 0 gates the drop on the breaker
/// having that quest started (see <c>OdinReactorActionManager.dropItems</c>).
/// </summary>
public sealed record ReactorDropEntry(int ItemId, int Chance, int QuestId);

/// <summary>Provides a reactor's drop table by its template (reactor) id.</summary>
public interface IReactorDropProvider
{
    /// <summary>The drop entries for a reactor, or an empty list when it has none.</summary>
    IReadOnlyList<ReactorDropEntry> GetDrops(int reactorId);
}

/// <summary>
/// A reactor-drop provider backed by the reference <c>init_data_set.sql</c> dump's
/// <c>reactordrops</c> table (columns <c>reactordropid, reactorid, itemid, chance, questid</c>).
/// Only the tuples inside <c>INSERT INTO `reactordrops`</c> statements are read — the dump holds
/// many other tables.
/// </summary>
public sealed class SqlReactorDropProvider : IReactorDropProvider
{
    // The whole VALUES payload of one reactordrops INSERT statement.
    private static readonly Regex InsertPattern = new(
        @"INSERT INTO `reactordrops`[^;]*;",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    // One tuple: (reactordropid, reactorid, itemid, chance, questid).
    private static readonly Regex RowPattern = new(
        @"\(\s*-?\d+\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IReadOnlyDictionary<int, List<ReactorDropEntry>> _byReactor;

    private SqlReactorDropProvider(IReadOnlyDictionary<int, List<ReactorDropEntry>> byReactor)
        => _byReactor = byReactor;

    public IReadOnlyList<ReactorDropEntry> GetDrops(int reactorId)
        => _byReactor.TryGetValue(reactorId, out List<ReactorDropEntry>? drops) ? drops : Array.Empty<ReactorDropEntry>();

    /// <summary>Loads reactor drops from an <c>init_data_set.sql</c> file (empty if missing).</summary>
    public static SqlReactorDropProvider LoadFile(string path)
        => File.Exists(path) ? Parse(File.ReadAllText(path)) : new SqlReactorDropProvider(new Dictionary<int, List<ReactorDropEntry>>());

    /// <summary>Parses the <c>reactordrops</c> INSERT tuples into a reactor-keyed map.</summary>
    public static SqlReactorDropProvider Parse(string sqlText)
    {
        var map = new Dictionary<int, List<ReactorDropEntry>>();
        foreach (Match insert in InsertPattern.Matches(sqlText))
        {
            foreach (Match m in RowPattern.Matches(insert.Value))
            {
                int reactorId = int.Parse(m.Groups[1].ValueSpan);
                int itemId = int.Parse(m.Groups[2].ValueSpan);
                int chance = int.Parse(m.Groups[3].ValueSpan);
                int questId = int.Parse(m.Groups[4].ValueSpan);

                if (!map.TryGetValue(reactorId, out List<ReactorDropEntry>? list))
                {
                    list = new List<ReactorDropEntry>();
                    map[reactorId] = list;
                }

                list.Add(new ReactorDropEntry(itemId, chance, questId));
            }
        }

        return new SqlReactorDropProvider(map);
    }
}

/// <summary>An in-memory reactor-drop provider for tests / seeded content.</summary>
public sealed class InMemoryReactorDropProvider : IReactorDropProvider
{
    private readonly IReadOnlyDictionary<int, List<ReactorDropEntry>> _byReactor;

    public InMemoryReactorDropProvider(IReadOnlyDictionary<int, IReadOnlyList<ReactorDropEntry>> byReactor)
        => _byReactor = byReactor.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());

    public IReadOnlyList<ReactorDropEntry> GetDrops(int reactorId)
        => _byReactor.TryGetValue(reactorId, out List<ReactorDropEntry>? drops) ? drops : Array.Empty<ReactorDropEntry>();
}
