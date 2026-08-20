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

/// <summary>An NPC placed on a map (a <c>life</c> entry of type "n"; ports TacosMapData.loadLife).</summary>
public sealed class NpcSpawn
{
    /// <summary>NPC template id (<c>id</c>) — also the script key.</summary>
    public required int TemplateId { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    /// <summary>Foothold (<c>fh</c>).</summary>
    public int Foothold { get; init; }

    /// <summary>Facing (<c>f</c>), flipped from wz to packet convention (1 → 0, else 1).</summary>
    public int Facing { get; init; }

    /// <summary>Horizontal range low (<c>rx0</c>).</summary>
    public int Rx0 { get; init; }

    /// <summary>Horizontal range high (<c>rx1</c>).</summary>
    public int Rx1 { get; init; }

    public bool Hidden { get; init; }
}

/// <summary>A monster placed on a map (a <c>life</c> entry of type "m").</summary>
public sealed class MobSpawn
{
    public required int TemplateId { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    /// <summary>Foothold (<c>fh</c>).</summary>
    public int Foothold { get; init; }

    /// <summary>Max HP. Real values come from mob wz data; defaulted until that loads.</summary>
    public int MaxHp { get; init; } = 100;

    public bool Hidden { get; init; }
}

/// <summary>Static data for one map: its portals (ports the server-relevant subset of a Map .img).</summary>
public sealed class MapData
{
    public const int NoLink = 999999999;

    public required int MapId { get; init; }

    public required IReadOnlyList<PortalData> Portals { get; init; }

    public IReadOnlyList<NpcSpawn> Npcs { get; init; } = Array.Empty<NpcSpawn>();

    public IReadOnlyList<MobSpawn> Mobs { get; init; } = Array.Empty<MobSpawn>();

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

        var npcs = new List<NpcSpawn>();
        var mobs = new List<MobSpawn>();
        WzData? lifeRoot = mapImg.Child("life");
        if (lifeRoot is not null)
        {
            foreach (WzData entry in lifeRoot.Children.Values)
            {
                string type = entry.GetString("type");
                int templateId = entry.GetInt("id", -1);
                if (templateId < 0)
                {
                    continue;
                }

                bool hidden = entry.GetInt("hide") == 1;
                if (type == "n")
                {
                    int wzFacing = entry.GetInt("f");
                    npcs.Add(new NpcSpawn
                    {
                        TemplateId = templateId,
                        X = entry.GetInt("x"),
                        Y = entry.GetInt("y"),
                        Foothold = entry.GetInt("fh"),
                        Facing = wzFacing == 1 ? 0 : 1, // wz left/right -> packet left/right
                        Rx0 = entry.GetInt("rx0"),
                        Rx1 = entry.GetInt("rx1"),
                        Hidden = hidden,
                    });
                }
                else if (type == "m")
                {
                    mobs.Add(new MobSpawn
                    {
                        TemplateId = templateId,
                        X = entry.GetInt("x"),
                        Y = entry.GetInt("y"),
                        Foothold = entry.GetInt("fh"),
                        Hidden = hidden,
                    });
                }
            }
        }

        return new MapData { MapId = mapId, Portals = portals, Npcs = npcs, Mobs = mobs };
    }
}
