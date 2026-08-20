using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;

namespace Cronus.Server.Channel;

/// <summary>A spawned monster in a field: a runtime object id bound to a wz template + placement.</summary>
public sealed class FieldMob
{
    public required int ObjectId { get; init; }

    public required int TemplateId { get; init; }

    public short X { get; init; }

    public short Y { get; init; }

    public int Foothold { get; init; }
}

/// <summary>A spawned NPC in a field: a runtime object id bound to a wz template + placement.</summary>
public sealed class FieldNpc
{
    public required int ObjectId { get; init; }

    public required int TemplateId { get; init; }

    public short X { get; init; }

    public short Y { get; init; }

    public int Facing { get; init; }

    public int Foothold { get; init; }

    public int Rx0 { get; init; }

    public int Rx1 { get; init; }
}

/// <summary>A player present in a field: the character plus their live session and position.</summary>
public sealed class FieldPlayer
{
    public FieldPlayer(Character character, MapleSession session)
    {
        Character = character;
        Session = session;
        X = 0;
        Y = 0;
    }

    public Character Character { get; }

    public MapleSession Session { get; }

    public short X { get; set; }

    public short Y { get; set; }

    public byte Stance { get; set; }
}

/// <summary>
/// One map instance: the players inside it and broadcast helpers (ports the broadcast role of
/// <c>odin.server.maps.MapleMap</c>, minus map data which arrives with Cronus.Data).
/// </summary>
public sealed class Field
{
    /// <summary>Object-id bases, kept clear of DB character ids and of each other.</summary>
    private const int NpcObjectIdBase = 1_000_000;
    private const int MobObjectIdBase = 2_000_000;

    private readonly Dictionary<int, FieldPlayer> _players = new();
    private readonly object _gate = new();

    public Field(int mapId, MapData? mapData = null)
    {
        MapId = mapId;
        Npcs = BuildNpcs(mapData);
        Mobs = BuildMobs(mapData);
    }

    public int MapId { get; }

    /// <summary>NPCs spawned on this field (from wz life data); empty when no map data.</summary>
    public IReadOnlyList<FieldNpc> Npcs { get; }

    /// <summary>Monsters spawned on this field (from wz life data); empty when no map data.</summary>
    public IReadOnlyList<FieldMob> Mobs { get; }

    /// <summary>Finds a spawned NPC by its runtime object id.</summary>
    public FieldNpc? FindNpc(int objectId)
    {
        foreach (FieldNpc npc in Npcs)
        {
            if (npc.ObjectId == objectId)
            {
                return npc;
            }
        }

        return null;
    }

    private static IReadOnlyList<FieldMob> BuildMobs(MapData? mapData)
    {
        if (mapData is null || mapData.Mobs.Count == 0)
        {
            return Array.Empty<FieldMob>();
        }

        var mobs = new List<FieldMob>(mapData.Mobs.Count);
        int oid = MobObjectIdBase;
        foreach (MobSpawn spawn in mapData.Mobs)
        {
            if (spawn.Hidden)
            {
                continue;
            }

            mobs.Add(new FieldMob
            {
                ObjectId = oid++,
                TemplateId = spawn.TemplateId,
                X = (short)spawn.X,
                Y = (short)spawn.Y,
                Foothold = spawn.Foothold,
            });
        }

        return mobs;
    }

    private static IReadOnlyList<FieldNpc> BuildNpcs(MapData? mapData)
    {
        if (mapData is null || mapData.Npcs.Count == 0)
        {
            return Array.Empty<FieldNpc>();
        }

        var npcs = new List<FieldNpc>(mapData.Npcs.Count);
        int oid = NpcObjectIdBase;
        foreach (NpcSpawn spawn in mapData.Npcs)
        {
            if (spawn.Hidden)
            {
                continue;
            }

            npcs.Add(new FieldNpc
            {
                ObjectId = oid++,
                TemplateId = spawn.TemplateId,
                X = (short)spawn.X,
                Y = (short)spawn.Y,
                Facing = spawn.Facing,
                Foothold = spawn.Foothold,
                Rx0 = spawn.Rx0,
                Rx1 = spawn.Rx1,
            });
        }

        return npcs;
    }

    public IReadOnlyList<FieldPlayer> Players
    {
        get
        {
            lock (_gate)
            {
                return _players.Values.ToList();
            }
        }
    }

    public void Enter(FieldPlayer player)
    {
        lock (_gate)
        {
            _players[player.Character.Id] = player;
        }
    }

    public FieldPlayer? Leave(int characterId)
    {
        lock (_gate)
        {
            if (_players.Remove(characterId, out FieldPlayer? player))
            {
                return player;
            }

            return null;
        }
    }

    /// <summary>Sends <paramref name="packet"/> to everyone except <paramref name="exceptCharacterId"/>.</summary>
    public async ValueTask BroadcastAsync(byte[] packet, int exceptCharacterId = -1)
    {
        foreach (FieldPlayer player in Players)
        {
            if (player.Character.Id == exceptCharacterId)
            {
                continue;
            }

            try
            {
                await player.Session.SendAsync(packet).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A dead session drops out on its own disconnect path; keep broadcasting.
            }
        }
    }
}

/// <summary>Fields by map id, created on demand (NPCs populated from the map provider).</summary>
public sealed class FieldRegistry
{
    private readonly Dictionary<int, Field> _fields = new();
    private readonly object _gate = new();
    private readonly IMapProvider? _maps;

    public FieldRegistry(IMapProvider? maps = null) => _maps = maps;

    public Field Get(int mapId)
    {
        lock (_gate)
        {
            if (!_fields.TryGetValue(mapId, out Field? field))
            {
                field = new Field(mapId, _maps?.GetMap(mapId));
                _fields[mapId] = field;
            }

            return field;
        }
    }
}
