using System.Text.RegularExpressions;

namespace Cronus.Data;

/// <summary>One warpable map: its id plus the two names the client shows for it.</summary>
public sealed record MapEntry(int MapId, string StreetName, string MapName)
{
    /// <summary>The menu label — "street : map" when they differ, otherwise just the map name.</summary>
    public string DisplayName => StreetName.Length == 0 || StreetName == MapName
        ? MapName
        : $"{StreetName} : {MapName}";
}

/// <summary>The maps sharing one street name ("ヘネシス", "オルビス", …), in ascending id order.</summary>
public sealed record MapStreet(string Name, IReadOnlyList<MapEntry> Maps);

/// <summary>
/// One browsable region (a top-level String/Map.img group), split into its streets. The two-level
/// split is what makes browsing practical: a region can hold over a thousand maps, but a street
/// is usually a handful — and streets are how the game itself names places.
/// </summary>
public sealed record MapRegion(string Key, string DisplayName, IReadOnlyList<MapStreet> Streets)
{
    /// <summary>Every map in the region, flattened (street order, then id order).</summary>
    public IEnumerable<MapEntry> Maps => Streets.SelectMany(s => s.Maps);

    /// <summary>How many maps the region holds, for the menu label.</summary>
    public int MapCount => Streets.Sum(s => s.Maps.Count);
}

/// <summary>Enumerates every named map the game data knows, grouped for browsing.</summary>
public interface IMapCatalog
{
    /// <summary>The non-empty regions, in menu order.</summary>
    IReadOnlyList<MapRegion> Regions { get; }
}

/// <summary>
/// Builds the catalog from <c>String.wz/Map.img.xml</c> — the same name table the client shows —
/// intersected with the maps that really exist in <c>Map.wz</c> (<c>Map/Map{n}/{id}.img.xml</c>).
/// The intersection matters for the same reason it does in <see cref="WzItemCatalog"/>: the string
/// table names ids that carry no data, and warping to one would leave the client with nothing to
/// draw. Parsed once, lazily.
/// </summary>
public sealed class WzMapCatalog : IMapCatalog
{
    /// <summary>A top-level region group inside Map.img.xml (maple / victoria / ossyria / …).</summary>
    private static readonly Regex RegionGroup = new("<imgdir name=\"([a-z][a-zA-Z]*)\">", RegexOptions.Compiled);

    /// <summary>One map entry: a numeric imgdir holding streetName/mapName strings. Ids run 1-9
    /// digits — the tutorial maps are 0-3.</summary>
    private static readonly Regex MapEntryTag = new(
        "<imgdir name=\"([0-9]{1,9})\">(.*?)</imgdir>", RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex NameValue = new(
        "<string name=\"(streetName|mapName)\" value=\"([^\"]*)\"", RegexOptions.Compiled);

    /// <summary>Region keys in menu order, with their Japanese labels. Keys absent from the data
    /// (or whose maps all turn out to be phantom) simply drop out of the menu.</summary>
    private static readonly (string Key, string Label)[] RegionLabels =
    {
        ("maple", "メイプルアイランド"),
        ("victoria", "ビクトリアアイランド"),
        ("ossyria", "オシリア大陸"),
        ("elin", "エリンの森"),
        ("jp", "日本エリア"),
        ("china", "中国エリア"),
        ("taiwan", "台湾エリア"),
        ("thai", "タイエリア"),
        ("wedding", "結婚式場"),
        ("etc", "その他"),
    };

    private readonly Lazy<IReadOnlyList<MapRegion>> _regions;

    public WzMapCatalog(string wzRoot) => _regions = new(() => Load(wzRoot));

    public IReadOnlyList<MapRegion> Regions => _regions.Value;

    private static IReadOnlyList<MapRegion> Load(string wzRoot)
    {
        string namesPath = Path.Combine(wzRoot, "String", "Map.img.xml");
        if (!File.Exists(namesPath))
        {
            return Array.Empty<MapRegion>();
        }

        string xml = File.ReadAllText(namesPath);
        HashSet<int> real = RealMapIds(wzRoot);
        var regions = new List<MapRegion>();

        foreach ((string key, string label) in RegionLabels)
        {
            var streets = StreetsInRegion(xml, key, real);
            if (streets.Count > 0)
            {
                regions.Add(new MapRegion(key, label, streets));
            }
        }

        return regions;
    }

    /// <summary>Every map id that has real field data — the client has the same files, so an id in
    /// here is one it can actually load.</summary>
    private static HashSet<int> RealMapIds(string wzRoot)
    {
        var ids = new HashSet<int>();
        string mapRoot = Path.Combine(wzRoot, "Map");
        if (!Directory.Exists(mapRoot))
        {
            return ids;
        }

        foreach (string file in Directory.EnumerateFiles(mapRoot, "*.img.xml", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(file);
            int dot = name.IndexOf('.');
            if (dot > 0 && int.TryParse(name.AsSpan(0, dot), out int id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// The playable maps inside one region group (up to the next region header), grouped by street
    /// name. Street order follows the lowest map id in each street, so the menu reads roughly in
    /// the order the areas were added to the game.
    /// </summary>
    private static IReadOnlyList<MapStreet> StreetsInRegion(string xml, string region, HashSet<int> real)
    {
        Match start = RegionGroup.Matches(xml).FirstOrDefault(m => m.Groups[1].Value == region);
        if (start is null)
        {
            return Array.Empty<MapStreet>();
        }

        int from = start.Index + start.Length;
        Match next = RegionGroup.Match(xml, from);
        string span = next.Success ? xml[from..next.Index] : xml[from..];

        var byStreet = new Dictionary<string, SortedDictionary<int, MapEntry>>();
        foreach (Match entry in MapEntryTag.Matches(span))
        {
            if (!int.TryParse(entry.Groups[1].ValueSpan, out int id) || !real.Contains(id))
            {
                continue;
            }

            string street = string.Empty;
            string map = string.Empty;
            foreach (Match nv in NameValue.Matches(entry.Groups[2].Value))
            {
                if (nv.Groups[1].Value == "streetName")
                {
                    street = nv.Groups[2].Value;
                }
                else
                {
                    map = nv.Groups[2].Value;
                }
            }

            if (map.Length == 0)
            {
                continue; // unnamed entry — nothing to show in a menu
            }

            string key = street.Length > 0 ? street : map;
            if (!byStreet.TryGetValue(key, out SortedDictionary<int, MapEntry>? maps))
            {
                byStreet[key] = maps = new SortedDictionary<int, MapEntry>();
            }

            maps[id] = new MapEntry(id, street, map);
        }

        return byStreet
            .OrderBy(kv => kv.Value.Keys.First())
            .Select(kv => new MapStreet(kv.Key, kv.Value.Values.ToList()))
            .ToList();
    }
}

/// <summary>An in-memory catalog for tests / seeded content.</summary>
public sealed class InMemoryMapCatalog : IMapCatalog
{
    public InMemoryMapCatalog(IEnumerable<MapRegion> regions) => Regions = regions.ToList();

    public IReadOnlyList<MapRegion> Regions { get; }
}
