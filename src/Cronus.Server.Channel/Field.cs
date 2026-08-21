using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;

namespace Cronus.Server.Channel;

/// <summary>A meso drop lying on the field.</summary>
public sealed class FieldDrop
{
    public required int ObjectId { get; init; }

    public required int Meso { get; init; }

    public short X { get; init; }

    public short Y { get; init; }

    /// <summary>The mob the drop came from (drop-from position + source id).</summary>
    public int SourceObjectId { get; init; }

    public short SourceX { get; init; }

    public short SourceY { get; init; }
}

/// <summary>A spawned monster in a field: a runtime object id bound to a wz template + placement.</summary>
public sealed class FieldMob
{
    public required int ObjectId { get; init; }

    public required int TemplateId { get; init; }

    public short X { get; set; }

    public short Y { get; set; }

    public int Foothold { get; init; }

    public int MaxHp { get; init; } = 100;

    public int Hp { get; set; } = 100;

    public short Mp { get; set; }

    /// <summary>Experience granted on kill (from mob wz data; 0 if unknown).</summary>
    public int Exp { get; init; }

    /// <summary>Respawn time in seconds from the map spawn: &gt;0 delay, -1 never, 0 = default.</summary>
    public int MobTime { get; init; }

    /// <summary>
    /// Character id of the client simulating this mob's movement, or -1 when uncontrolled.
    /// MapleStory delegates mob AI to one nearby client; the server acks and relays.
    /// </summary>
    public int ControllerId { get; set; } = -1;

    /// <summary>
    /// <see cref="Environment.TickCount64"/> at which a dead mob should respawn, or 0 when it is
    /// alive / not scheduled. Set on death; cleared by <see cref="Respawn"/>.
    /// </summary>
    public long RespawnAtTick { get; set; }

    public bool IsDead => Hp <= 0;

    /// <summary>Applies damage, returns the new HP (clamped at 0).</summary>
    public int Damage(int amount)
    {
        Hp = Math.Max(0, Hp - Math.Max(0, amount));
        return Hp;
    }

    /// <summary>Brings a dead mob back to full HP at its spawn point (uncontrolled, unscheduled).</summary>
    public void Respawn()
    {
        Hp = MaxHp;
        ControllerId = -1;
        RespawnAtTick = 0;
    }
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

    /// <summary>
    /// <see cref="Environment.TickCount64"/> of the player's last move/attack. Natural HP/MP
    /// regen only kicks in after they've been idle for a bit (see <c>PlayerRegenService</c>).
    /// </summary>
    public long LastActiveTick { get; set; }
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
    private const int DropObjectIdBase = 3_000_000;

    private readonly Dictionary<int, FieldPlayer> _players = new();
    private readonly Dictionary<int, FieldDrop> _drops = new();
    private int _nextDropOid = DropObjectIdBase;
    private readonly object _gate = new();

    public Field(int mapId, MapData? mapData = null, IMobProvider? mobs = null)
    {
        MapId = mapId;
        Npcs = BuildNpcs(mapData);
        Mobs = BuildMobs(mapData, mobs);
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

    /// <summary>Finds a spawned (live or dead) monster by its runtime object id.</summary>
    public FieldMob? FindMob(int objectId)
    {
        foreach (FieldMob mob in Mobs)
        {
            if (mob.ObjectId == objectId)
            {
                return mob;
            }
        }

        return null;
    }

    /// <summary>
    /// Respawns any dead mobs whose scheduled respawn time has arrived and returns them for the
    /// caller (the server tick) to announce with <c>LP_MobEnterField</c>.
    /// </summary>
    public IReadOnlyList<FieldMob> TakeRespawnDueMobs(long nowTick)
    {
        List<FieldMob>? due = null;
        foreach (FieldMob mob in Mobs)
        {
            if (mob.IsDead && mob.RespawnAtTick != 0 && mob.RespawnAtTick <= nowTick)
            {
                mob.Respawn();
                (due ??= new List<FieldMob>()).Add(mob);
            }
        }

        return due ?? (IReadOnlyList<FieldMob>)Array.Empty<FieldMob>();
    }

    private static IReadOnlyList<FieldMob> BuildMobs(MapData? mapData, IMobProvider? mobProvider)
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

            // Prefer wz mob stats; fall back to the spawn's placeholder HP.
            MobData? stats = mobProvider?.GetMob(spawn.TemplateId);
            int maxHp = stats?.MaxHp ?? spawn.MaxHp;

            mobs.Add(new FieldMob
            {
                ObjectId = oid++,
                TemplateId = spawn.TemplateId,
                X = (short)spawn.X,
                Y = (short)spawn.Y,
                Foothold = spawn.Foothold,
                MaxHp = maxHp,
                Hp = maxHp,
                Exp = stats?.Exp ?? 0,
                MobTime = spawn.MobTime,
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

    /// <summary>Registers a meso drop and returns it (assigns the object id).</summary>
    public FieldDrop AddMesoDrop(int meso, short x, short y, FieldMob source)
    {
        lock (_gate)
        {
            var drop = new FieldDrop
            {
                ObjectId = _nextDropOid++,
                Meso = meso,
                X = x,
                Y = y,
                SourceObjectId = source.ObjectId,
                SourceX = source.X,
                SourceY = source.Y,
            };
            _drops[drop.ObjectId] = drop;
            return drop;
        }
    }

    /// <summary>Removes a drop by object id (e.g. on pickup); returns it if it was present.</summary>
    public FieldDrop? RemoveDrop(int objectId)
    {
        lock (_gate)
        {
            return _drops.Remove(objectId, out FieldDrop? drop) ? drop : null;
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
    private readonly IMobProvider? _mobs;

    public FieldRegistry(IMapProvider? maps = null, IMobProvider? mobs = null)
    {
        _maps = maps;
        _mobs = mobs;
    }

    public Field Get(int mapId)
    {
        lock (_gate)
        {
            if (!_fields.TryGetValue(mapId, out Field? field))
            {
                field = new Field(mapId, _maps?.GetMap(mapId), _mobs);
                _fields[mapId] = field;
            }

            return field;
        }
    }

    /// <summary>A snapshot of the currently active fields (for the server tick).</summary>
    public IReadOnlyList<Field> Fields
    {
        get
        {
            lock (_gate)
            {
                return _fields.Values.ToList();
            }
        }
    }
}
