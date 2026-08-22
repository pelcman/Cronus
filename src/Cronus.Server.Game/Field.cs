using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;

namespace Cronus.Server.Game;

/// <summary>
/// A drop lying on the field — a meso pile (<see cref="ItemId"/> == 0, amount in <see cref="Meso"/>)
/// or an item stack (<see cref="ItemId"/> &gt; 0, count in <see cref="Quantity"/>). The wire form
/// branches on which it is (<c>ResCDropPool.DropEnterField</c>).
/// </summary>
public sealed class FieldDrop
{
    public required int ObjectId { get; init; }

    /// <summary>Meso amount for a meso drop; 0 for an item drop.</summary>
    public int Meso { get; init; }

    /// <summary>Item id for an item drop; 0 for a meso drop.</summary>
    public int ItemId { get; init; }

    /// <summary>Stack count for an item drop (applied to inventory on pickup); 1 by default.</summary>
    public short Quantity { get; init; } = 1;

    /// <summary>
    /// The actual item instance riding this drop (player-thrown equips keep their stats through
    /// drop → pickup); null for generated drops (mob loot creates a fresh item on pickup).
    /// </summary>
    public InventoryItem? ItemInstance { get; init; }

    /// <summary>True when this is a meso pile rather than an item stack.</summary>
    public bool IsMeso => ItemId == 0;

    public short X { get; init; }

    public short Y { get; init; }

    /// <summary>The mob the drop came from (drop-from position + source id).</summary>
    public int SourceObjectId { get; init; }

    public short SourceX { get; init; }

    public short SourceY { get; init; }

    /// <summary><see cref="Environment.TickCount64"/> when the drop hit the ground (for expiry).</summary>
    public long DropAtTick { get; init; }

    /// <summary>True when a player threw this drop (vs. a mob dropping it); flips a wire byte.</summary>
    public bool IsPlayerDrop { get; init; }
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

    /// <summary>Boss HP-gauge tag colour (0 = ordinary mob, no gauge); marks a boss.</summary>
    public int TagColor { get; init; }

    /// <summary>Boss HP-gauge background colour.</summary>
    public int TagBgColor { get; init; }

    /// <summary>True when this mob shows the boss HP gauge (has a tag colour).</summary>
    public bool IsBoss => TagColor != 0;

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

    /// <summary>Per mob-skill cooldown bookkeeping: skill id → tick of the last cast.</summary>
    public Dictionary<int, long> LastSkillUse { get; } = new();

    /// <summary>Heals up to max (a mob-skill heal); returns the HP actually restored.</summary>
    public int Heal(int amount)
    {
        int restored = Math.Min(Math.Max(0, amount), MaxHp - Hp);
        Hp += restored;
        return restored;
    }

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

/// <summary>
/// A summoned pet following its owner (ports the runtime side of <c>MaplePet</c>): the backing
/// cash item (whose Pet* fields hold name/level/closeness) plus the live position the pet-move
/// packets keep fresh.
/// </summary>
public sealed class ActivePet
{
    public ActivePet(InventoryItem item, short x, short y)
    {
        Item = item;
        X = x;
        Y = y;
        UniqueId = item.Id != 0 ? item.Id : item.ItemId;
    }

    /// <summary>The pet cash item this pet lives on.</summary>
    public InventoryItem Item { get; }

    public long UniqueId { get; }

    public short X { get; set; }

    public short Y { get; set; }

    public byte Stance { get; set; }

    public short Foothold { get; set; }
}

/// <summary>A reactor standing in a field (a box, plant, lever …): its live state plus respawn
/// bookkeeping (ports the runtime side of <c>MapleReactor</c>).</summary>
public sealed class FieldReactor
{
    public required int ObjectId { get; init; }

    public required int ReactorId { get; init; }

    public short X { get; init; }

    public short Y { get; init; }

    public byte Facing { get; init; }

    public string Name { get; init; } = string.Empty;

    /// <summary>Respawn delay in seconds after breaking (0 → a short default).</summary>
    public int ReactorTime { get; init; }

    public byte State { get; set; }

    public bool IsDead { get; set; }

    /// <summary><see cref="Environment.TickCount64"/> at which to respawn, or 0.</summary>
    public long RespawnAtTick { get; set; }

    /// <summary>Breaks the reactor and schedules its respawn.</summary>
    public void Break(long nowTick)
    {
        IsDead = true;
        RespawnAtTick = nowTick + Math.Max(3, ReactorTime) * 1000L;
    }

    /// <summary>Brings the reactor back at state 0.</summary>
    public void Respawn()
    {
        State = 0;
        IsDead = false;
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

    /// <summary>True while the player is sitting on a chair — regen is faster and immediate.</summary>
    public bool Seated { get; set; }

    /// <summary>The portable chair item (301xxxx) the player is sitting on, or 0.</summary>
    public int PortableChair { get; set; }

    /// <summary>The player's summoned pet, or null (single pet, index 0).</summary>
    public ActivePet? Pet { get; set; }

    /// <summary>The ad board (黒板) message standing over the player, or null.</summary>
    public string? AdBoard { get; set; }

    /// <summary>Crusader combo orbs currently charged (0 when combo is off or fresh).</summary>
    public int ComboOrbs { get; set; }
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
    private const int ReactorObjectIdBase = 4_000_000;

    private readonly Dictionary<int, FieldPlayer> _players = new();
    private readonly Dictionary<int, FieldDrop> _drops = new();
    private int _nextDropOid = DropObjectIdBase;
    private readonly object _gate = new();

    public Field(int mapId, MapData? mapData = null, IMobProvider? mobs = null)
    {
        MapId = mapId;
        Npcs = BuildNpcs(mapData);
        _mobs = BuildMobs(mapData, mobs);
        _nextMobOid = MobObjectIdBase + _mobs.Count;
        Reactors = BuildReactors(mapData);
        Recovery = mapData?.Recovery ?? 1.0;
    }

    /// <summary>The map's natural HP/MP recovery multiplier (sauna rooms are 2x+).</summary>
    public double Recovery { get; }

    /// <summary>Reactors placed on this field (boxes, plants, …); empty when no map data.</summary>
    public IReadOnlyList<FieldReactor> Reactors { get; }

    public FieldReactor? FindReactor(int objectId)
    {
        foreach (FieldReactor reactor in Reactors)
        {
            if (reactor.ObjectId == objectId)
            {
                return reactor;
            }
        }

        return null;
    }

    /// <summary>Respawns broken reactors whose delay has passed; returns them for announcing.</summary>
    public IReadOnlyList<FieldReactor> TakeRespawnDueReactors(long nowTick)
    {
        List<FieldReactor>? due = null;
        foreach (FieldReactor reactor in Reactors)
        {
            if (reactor.IsDead && reactor.RespawnAtTick != 0 && reactor.RespawnAtTick <= nowTick)
            {
                reactor.Respawn();
                (due ??= new List<FieldReactor>()).Add(reactor);
            }
        }

        return due ?? (IReadOnlyList<FieldReactor>)Array.Empty<FieldReactor>();
    }

    private static IReadOnlyList<FieldReactor> BuildReactors(MapData? mapData)
    {
        if (mapData is null || mapData.Reactors.Count == 0)
        {
            return Array.Empty<FieldReactor>();
        }

        var reactors = new List<FieldReactor>(mapData.Reactors.Count);
        int oid = ReactorObjectIdBase;
        foreach (ReactorSpawn spawn in mapData.Reactors)
        {
            reactors.Add(new FieldReactor
            {
                ObjectId = oid++,
                ReactorId = spawn.ReactorId,
                X = (short)spawn.X,
                Y = (short)spawn.Y,
                Facing = (byte)spawn.Facing,
                Name = spawn.Name,
                ReactorTime = spawn.ReactorTime,
            });
        }

        return reactors;
    }

    public int MapId { get; }

    /// <summary>NPCs spawned on this field (from wz life data); empty when no map data.</summary>
    public IReadOnlyList<FieldNpc> Npcs { get; }

    private readonly List<FieldMob> _mobs;
    private int _nextMobOid;

    /// <summary>Monsters on this field (map spawns + live summons); a stable snapshot.</summary>
    public IReadOnlyList<FieldMob> Mobs
    {
        get
        {
            lock (_gate)
            {
                return _mobs.ToArray();
            }
        }
    }

    /// <summary>
    /// Adds a mob at runtime (a mob-skill summon). It never respawns on its own (MobTime -1);
    /// the caller announces it with <c>LP_MobEnterField</c>.
    /// </summary>
    public FieldMob SpawnMob(int templateId, MobData? stats, short x, short y, int foothold)
    {
        int maxHp = stats?.MaxHp ?? 100;
        lock (_gate)
        {
            var mob = new FieldMob
            {
                ObjectId = _nextMobOid++,
                TemplateId = templateId,
                X = x,
                Y = y,
                Foothold = foothold,
                MaxHp = maxHp,
                Hp = maxHp,
                Exp = stats?.Exp ?? 0,
                MobTime = -1,
                TagColor = stats?.TagColor ?? 0,
                TagBgColor = stats?.TagBgColor ?? 0,
            };
            _mobs.Add(mob);
            return mob;
        }
    }

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

    private static List<FieldMob> BuildMobs(MapData? mapData, IMobProvider? mobProvider)
    {
        if (mapData is null || mapData.Mobs.Count == 0)
        {
            return new List<FieldMob>();
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
                TagColor = stats?.TagColor ?? 0,
                TagBgColor = stats?.TagBgColor ?? 0,
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

    /// <summary>A snapshot of the drops currently on the ground (for spawning to a newcomer).</summary>
    public IReadOnlyList<FieldDrop> Drops
    {
        get
        {
            lock (_gate)
            {
                return _drops.Values.ToList();
            }
        }
    }

    /// <summary>Registers a meso drop from a killed mob and returns it (assigns the object id).</summary>
    public FieldDrop AddMesoDrop(int meso, short x, short y, FieldMob source)
        => AddMesoDropCore(meso, x, y, source.ObjectId, source.X, source.Y, playerDrop: false);

    /// <summary>
    /// Registers a meso drop thrown by a player at their own position, and returns it (the drop
    /// falls from and lands at the player's feet; others can pick it up).
    /// </summary>
    public FieldDrop AddPlayerMesoDrop(int meso, short x, short y, int sourceCharacterId)
        => AddMesoDropCore(meso, x, y, sourceCharacterId, x, y, playerDrop: true);

    private FieldDrop AddMesoDropCore(int meso, short x, short y, int sourceObjectId, short sourceX, short sourceY, bool playerDrop)
    {
        lock (_gate)
        {
            var drop = new FieldDrop
            {
                ObjectId = _nextDropOid++,
                Meso = meso,
                X = x,
                Y = y,
                SourceObjectId = sourceObjectId,
                SourceX = sourceX,
                SourceY = sourceY,
                DropAtTick = Environment.TickCount64,
                IsPlayerDrop = playerDrop,
            };
            _drops[drop.ObjectId] = drop;
            return drop;
        }
    }

    /// <summary>Registers an item drop from a killed mob and returns it (assigns the object id).</summary>
    public FieldDrop AddItemDrop(int itemId, short quantity, short x, short y, FieldMob source)
    {
        lock (_gate)
        {
            var drop = new FieldDrop
            {
                ObjectId = _nextDropOid++,
                ItemId = itemId,
                Quantity = quantity < 1 ? (short)1 : quantity,
                X = x,
                Y = y,
                SourceObjectId = source.ObjectId,
                SourceX = source.X,
                SourceY = source.Y,
                DropAtTick = Environment.TickCount64,
                IsPlayerDrop = false,
            };
            _drops[drop.ObjectId] = drop;
            return drop;
        }
    }

    /// <summary>
    /// Registers an item stack a player threw onto the ground at their own position, and returns it
    /// (others can pick it up); the drop-from source is the player. When
    /// <paramref name="instance"/> is given, the drop carries that exact item (equips keep stats).
    /// </summary>
    public FieldDrop AddPlayerItemDrop(int itemId, short quantity, short x, short y, int sourceCharacterId, InventoryItem? instance = null)
    {
        lock (_gate)
        {
            var drop = new FieldDrop
            {
                ObjectId = _nextDropOid++,
                ItemId = itemId,
                Quantity = quantity < 1 ? (short)1 : quantity,
                ItemInstance = instance,
                X = x,
                Y = y,
                SourceObjectId = sourceCharacterId,
                SourceX = x,
                SourceY = y,
                DropAtTick = Environment.TickCount64,
                IsPlayerDrop = true,
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

    /// <summary>
    /// Removes drops that have been on the ground at least <paramref name="ttlMs"/> and returns
    /// their object ids, for the caller (the world tick) to fade with <c>LP_DropLeaveField</c>.
    /// </summary>
    public IReadOnlyList<int> RemoveExpiredDrops(long nowTick, long ttlMs)
    {
        lock (_gate)
        {
            List<int>? expired = null;
            foreach (FieldDrop drop in _drops.Values)
            {
                if (nowTick - drop.DropAtTick >= ttlMs)
                {
                    (expired ??= new List<int>()).Add(drop.ObjectId);
                }
            }

            if (expired is null)
            {
                return Array.Empty<int>();
            }

            foreach (int oid in expired)
            {
                _drops.Remove(oid);
            }

            return expired;
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

    /// <summary>The mob-template source shared by all fields (for summons), or null.</summary>
    public IMobProvider? MobProvider => _mobs;

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

    /// <summary>
    /// Finds an online player by character name across all active fields (case-insensitive), or
    /// null if nobody by that name is on this channel. Used for whisper / location lookups; a
    /// linear scan is fine for an in-group server's handful of players.
    /// </summary>
    public FieldPlayer? FindPlayerByName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return null;
        }

        foreach (Field field in Fields)
        {
            foreach (FieldPlayer player in field.Players)
            {
                if (string.Equals(player.Character.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return player;
                }
            }
        }

        return null;
    }
}
