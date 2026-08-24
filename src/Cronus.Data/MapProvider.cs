using System.Collections.Concurrent;

namespace Cronus.Data;

/// <summary>Loads and caches <see cref="MapData"/> by map id.</summary>
public interface IMapProvider
{
    /// <summary>Returns the map's data, or null if it is unavailable.</summary>
    MapData? GetMap(int mapId);
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

    public InMemoryMapProvider(IEnumerable<MapData> maps)
        => _maps = maps.ToDictionary(m => m.MapId);

    public MapData? GetMap(int mapId) => _maps.TryGetValue(mapId, out MapData? map) ? map : null;
}
