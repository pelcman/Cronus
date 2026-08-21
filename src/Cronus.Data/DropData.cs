using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Cronus.Data;

/// <summary>
/// One row of a mob's drop table (ports <c>odin.server.life.MonsterDropEntry</c> + the
/// <c>drop_data</c> schema). <see cref="ItemId"/> == 0 denotes a meso drop; otherwise it is the
/// dropped item id. <see cref="Chance"/> is an x/1000 probability (the roll is
/// <c>rand(0..999) &lt; chance</c>; see <c>TacosReward.dropFromDatabase</c>).
/// </summary>
public sealed record DropEntry(int ItemId, int MinQuantity, int MaxQuantity, int QuestId, int Chance);

/// <summary>Provides a mob's drop table by its template (dropper) id.</summary>
public interface IDropProvider
{
    /// <summary>The drop entries for a dropper mob, or an empty list when the mob has no table.</summary>
    IReadOnlyList<DropEntry> GetDrops(int dropperId);
}

/// <summary>
/// A drop provider backed by the reference <c>drop_data.sql</c> dump (columns
/// <c>id, dropperid, itemid, minimum_quantity, maximum_quantity, questid, chance</c>). The dump is
/// parsed once into a <c>dropperId → entries</c> map; the file is the same asset the Java build
/// loads into its <c>drop_data</c> table (see <c>MapleMonsterInformationProvider.retrieveDrop</c>).
/// </summary>
public sealed class SqlDropProvider : IDropProvider
{
    // Matches one VALUES tuple: (id, dropperid, itemid, min, max, questid, chance).
    private static readonly Regex RowPattern = new(
        @"\(\s*-?\d+\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IReadOnlyDictionary<int, List<DropEntry>> _byDropper;

    private SqlDropProvider(IReadOnlyDictionary<int, List<DropEntry>> byDropper) => _byDropper = byDropper;

    public IReadOnlyList<DropEntry> GetDrops(int dropperId)
        => _byDropper.TryGetValue(dropperId, out List<DropEntry>? drops) ? drops : Array.Empty<DropEntry>();

    /// <summary>Loads the drop table from a <c>drop_data.sql</c> file (empty provider if missing).</summary>
    public static SqlDropProvider LoadFile(string path)
        => File.Exists(path) ? Parse(File.ReadAllText(path)) : new SqlDropProvider(new Dictionary<int, List<DropEntry>>());

    /// <summary>Parses the INSERT tuples of a <c>drop_data</c> dump into a dropper-keyed map.</summary>
    public static SqlDropProvider Parse(string sqlText)
    {
        var map = new Dictionary<int, List<DropEntry>>();
        foreach (Match m in RowPattern.Matches(sqlText))
        {
            int dropperId = int.Parse(m.Groups[1].ValueSpan);
            int itemId = int.Parse(m.Groups[2].ValueSpan);
            int min = int.Parse(m.Groups[3].ValueSpan);
            int max = int.Parse(m.Groups[4].ValueSpan);
            int questId = int.Parse(m.Groups[5].ValueSpan);
            int chance = int.Parse(m.Groups[6].ValueSpan);

            if (!map.TryGetValue(dropperId, out List<DropEntry>? list))
            {
                list = new List<DropEntry>();
                map[dropperId] = list;
            }

            list.Add(new DropEntry(itemId, min, max, questId, chance));
        }

        return new SqlDropProvider(map);
    }
}

/// <summary>An in-memory drop provider for tests / seeded content.</summary>
public sealed class InMemoryDropProvider : IDropProvider
{
    private readonly IReadOnlyDictionary<int, List<DropEntry>> _byDropper;

    public InMemoryDropProvider(IReadOnlyDictionary<int, IReadOnlyList<DropEntry>> byDropper)
        => _byDropper = byDropper.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());

    public IReadOnlyList<DropEntry> GetDrops(int dropperId)
        => _byDropper.TryGetValue(dropperId, out List<DropEntry>? drops) ? drops : Array.Empty<DropEntry>();
}
