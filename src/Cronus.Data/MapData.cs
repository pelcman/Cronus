namespace Cronus.Data;

/// <summary>A portal within a map (ports the fields read by <c>TacosMapData.loadPortals</c>).</summary>
public sealed class PortalData
{
    public required int Id { get; init; }

    /// <summary>Portal type (<c>pt</c>). 0 = spawn point, 2 = normal portal, etc.</summary>
    public int Type { get; init; }

    /// <summary>Portal name (<c>pn</c>), e.g. "sp", "east00".</summary>
    public required string Name { get; init; }

    /// <summary>Target portal name in the destination map (<c>tn</c>).</summary>
    public string TargetName { get; init; } = string.Empty;

    /// <summary>Target map id (<c>tm</c>); 999999999 means "no link".</summary>
    public int TargetMapId { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    /// <summary>True when this portal links to another map.</summary>
    public bool LinksToMap => TargetMapId is not (0 or 999999999);
}

/// <summary>Static data for one map: its portals (ports the server-relevant subset of a Map .img).</summary>
public sealed class MapData
{
    public const int NoLink = 999999999;

    public required int MapId { get; init; }

    public required IReadOnlyList<PortalData> Portals { get; init; }

    /// <summary>The spawn portal (<c>pn == "sp"</c>) or the first portal, or null if none.</summary>
    public PortalData? SpawnPortal =>
        Portals.FirstOrDefault(p => p.Name == "sp") ?? Portals.FirstOrDefault();

    /// <summary>Finds a portal by name (case-sensitive, as the client sends it).</summary>
    public PortalData? FindPortal(string name)
        => Portals.FirstOrDefault(p => p.Name == name);

    /// <summary>
    /// Parses a Map <c>.img</c> WZ document into <see cref="MapData"/>. Reads the
    /// <c>portal</c> subtree; other subtrees (life, foothold, …) are ignored for now.
    /// </summary>
    public static MapData FromWz(int mapId, WzData mapImg)
    {
        var portals = new List<PortalData>();
        WzData? portalRoot = mapImg.Child("portal");
        if (portalRoot is not null)
        {
            foreach (WzData entry in portalRoot.Children.Values)
            {
                if (!int.TryParse(entry.Name, out int index))
                {
                    continue;
                }

                portals.Add(new PortalData
                {
                    Id = index,
                    Type = entry.GetInt("pt"),
                    Name = entry.GetString("pn"),
                    TargetName = entry.GetString("tn"),
                    TargetMapId = entry.GetInt("tm", NoLink),
                    X = entry.GetInt("x"),
                    Y = entry.GetInt("y"),
                });
            }
        }

        portals.Sort((a, b) => a.Id.CompareTo(b.Id));
        return new MapData { MapId = mapId, Portals = portals };
    }
}
