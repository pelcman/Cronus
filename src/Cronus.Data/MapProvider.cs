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
    private readonly string _wzRoot;
    private readonly ConcurrentDictionary<int, MapData?> _cache = new();

    public WzMapProvider(string wzRoot) => _wzRoot = wzRoot;

    public MapData? GetMap(int mapId) => _cache.GetOrAdd(mapId, Load);

    private MapData? Load(int mapId)
    {
        string path = MapImagePath(_wzRoot, mapId);
        if (!File.Exists(path))
        {
            return null;
        }

        WzData img = WzData.ParseFile(path);
        return MapData.FromWz(mapId, img);
    }

    /// <summary>Builds the expected on-disk path for a map's WZ image.</summary>
    public static string MapImagePath(string wzRoot, int mapId)
    {
        int prefix = mapId / 100000000;
        string file = $"{mapId:000000000}.img.xml";
        return Path.Combine(wzRoot, "Map", $"Map{prefix}", file);
    }
}

/// <summary>An in-memory map provider for tests / seeded worlds.</summary>
public sealed class InMemoryMapProvider : IMapProvider
{
    private readonly Dictionary<int, MapData> _maps;

    public InMemoryMapProvider(IEnumerable<MapData> maps)
        => _maps = maps.ToDictionary(m => m.MapId);

    public MapData? GetMap(int mapId) => _maps.TryGetValue(mapId, out MapData? map) ? map : null;
}
