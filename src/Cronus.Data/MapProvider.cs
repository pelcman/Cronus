using System.Collections.Concurrent;

namespace Cronus.Data;

/// <summary>Loads and caches <see cref="MapData"/> by map id.</summary>
public interface IMapProvider
{
    /// <summary>Returns the map's data, or null if it is unavailable.</summary>
    MapData? GetMap(int mapId);

    /// <summary>
    /// True when this provider holds the complete map set, so a null from <see cref="GetMap"/>
    /// means "no such map" (the client would crash entering it) rather than "not loaded here".
    /// Seeded/test providers answer false unless built as authoritative.
    /// </summary>
    bool KnowsAllMaps { get; }
}

/// <summary>
/// Loads map data from a wz_xml directory tree. The path convention mirrors upstream:
/// <c>Map/Map{prefix}/{mapId:000000000}.img.xml</c>, where prefix = mapId / 100000000.
/// Missing files return null (the caller decides how to degrade). Results are cached.
/// </summary>
public sealed class WzMapProvider : IMapProvider
{
    private readonly IWzStore _store;
    private readonly ConcurrentDictionary<int, MapData?> _cache = new();

    public WzMapProvider(string wzRoot) : this(new DirectoryWzStore(wzRoot))
    {
    }

    public WzMapProvider(IWzStore store) => _store = store;

    public MapData? GetMap(int mapId) => _cache.GetOrAdd(mapId, Load);

    /// <summary>The client's own Map.wz: what is not here does not exist for the client either.</summary>
    public bool KnowsAllMaps => true;

    private MapData? Load(int mapId)
    {
        string? xml = _store.ReadText(MapImageRel(mapId));
        return xml is null ? null : MapData.FromWz(mapId, WzData.ParseText(xml));
    }

    /// <summary>The store-relative path of a map's WZ image.</summary>
    public static string MapImageRel(int mapId)
        => $"Map/Map{mapId / 100000000}/{mapId:000000000}.img.xml";

    /// <summary>Builds the expected on-disk path for a map's WZ image.</summary>
    public static string MapImagePath(string wzRoot, int mapId)
        => Path.Combine(wzRoot, MapImageRel(mapId).Replace('/', Path.DirectorySeparatorChar));
}

/// <summary>An in-memory map provider for tests / seeded worlds.</summary>
public sealed class InMemoryMapProvider : IMapProvider
{
    private readonly Dictionary<int, MapData> _maps;

    /// <param name="authoritative">Treat the given maps as the whole world (see <see cref="KnowsAllMaps"/>).</param>
    public InMemoryMapProvider(IEnumerable<MapData> maps, bool authoritative = false)
    {
        _maps = maps.ToDictionary(m => m.MapId);
        KnowsAllMaps = authoritative;
    }

    public MapData? GetMap(int mapId) => _maps.TryGetValue(mapId, out MapData? map) ? map : null;

    public bool KnowsAllMaps { get; }
}
