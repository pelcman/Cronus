using Cronus.Data;

namespace Cronus.Server.Game;

/// <summary>One airship route: where passengers wait, where they ride, where they land.</summary>
/// <remarks>
/// The waiting rooms and flight maps have NO portals to the next leg in the wz — the real game
/// moves passengers by server schedule. The station/flight-map handshake packets are ported from
/// the oracle (<c>ReqCField.OnContiState</c>); the timetable itself has no oracle
/// (<c>Continent.img</c> is absent from the reference), so it is authored: a 15-minute cycle,
/// boarding open for the first 10 minutes, then a 5-minute flight.
/// </remarks>
public sealed record AirshipRoute(
    string Id,
    string Name,
    int TicketItemId,
    int StationMapId,
    int WaitingRoomMapId,
    int FlightMapId,
    int CabinMapId,
    int ArrivalMapId)
{
    public static readonly AirshipRoute ElliniaToOrbis = new(
        "ellinia-orbis", "オルビス行き", TicketItemId: 4031045,
        StationMapId: 101000300, WaitingRoomMapId: 101000301,
        FlightMapId: 200090010, CabinMapId: 200090011, ArrivalMapId: 200000111);

    public static readonly AirshipRoute OrbisToEllinia = new(
        "orbis-ellinia", "エリニア行き", TicketItemId: 4031047,
        StationMapId: 200000100, WaitingRoomMapId: 200000112,
        FlightMapId: 200090000, CabinMapId: 200090001, ArrivalMapId: 101000300);

    public static readonly IReadOnlyList<AirshipRoute> All = new[] { ElliniaToOrbis, OrbisToEllinia };

    public static AirshipRoute? Find(string id)
        => All.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));
}

/// <summary>The Balrog ship, relative to the current flight.</summary>
public enum EnemyShipState
{
    /// <summary>Not (yet) here — or this flight isn't raided.</summary>
    None,

    /// <summary>Alongside: the client shows it (CONTI_MOBGEN) and the raiders are aboard.</summary>
    Present,

    /// <summary>Peeled away before landing (CONTI_MOBDESTROY).</summary>
    Gone,
}

/// <summary>Where the ship is in its cycle.</summary>
public enum AirshipPhase
{
    /// <summary>Docked at the station; the waiting room is open.</summary>
    Boarding,

    /// <summary>In the air; the waiting room is sealed, passengers are on the flight map.</summary>
    Flight,
}

/// <summary>
/// The timetable, as pure wall-clock arithmetic (no state to lose on restart): every route shares
/// one cycle so both ships leave together like the originals. Simplified from the era's
/// timetable (no oracle); the numbers live here so an operator can retune them.
/// </summary>
public static class AirshipSchedule
{
    /// <summary>Full cycle length.</summary>
    public static readonly TimeSpan Cycle = TimeSpan.FromMinutes(15);

    /// <summary>How long boarding stays open at the start of each cycle.</summary>
    public static readonly TimeSpan BoardingWindow = TimeSpan.FromMinutes(10);

    /// <summary>Flight length (the rest of the cycle).</summary>
    public static TimeSpan FlightTime => Cycle - BoardingWindow;

    /// <summary>How far into the flight the Balrog ship pulls alongside.</summary>
    public static readonly TimeSpan RaidStart = TimeSpan.FromSeconds(60);

    /// <summary>How long before landing the Balrog ship peels away (raiders vanish).</summary>
    public static readonly TimeSpan RaidEndBeforeArrival = TimeSpan.FromSeconds(30);

    /// <summary>The clock every airship decision reads — swap it in tests to pin a moment.</summary>
    public static Func<DateTime> Clock { get; set; } = () => DateTime.UtcNow;

    /// <summary>Which cycle (since the epoch) <paramref name="utcNow"/> falls in.</summary>
    public static long CycleIndex(DateTime utcNow) => utcNow.Ticks / Cycle.Ticks;

    /// <summary>Whether this cycle's flight is raided — decided once per cycle from its index, so
    /// the handler and the tick agree without shared state.</summary>
    public static bool IsRaidCycle(DateTime utcNow)
        => Cronus.Common.GameConstants.AirshipBalrogChance >= 1.0
           || new Random(unchecked((int)CycleIndex(utcNow))).NextDouble() < Cronus.Common.GameConstants.AirshipBalrogChance;

    /// <summary>Where the Balrog ship is right now, relative to the flight.</summary>
    public static EnemyShipState EnemyShipAt(DateTime utcNow)
    {
        TimeSpan elapsed = Elapsed(utcNow);
        if (elapsed < BoardingWindow || !IsRaidCycle(utcNow))
        {
            return EnemyShipState.None;
        }

        TimeSpan intoFlight = elapsed - BoardingWindow;
        if (intoFlight < RaidStart)
        {
            return EnemyShipState.None;          // calm skies, the raid hasn't come yet
        }

        return intoFlight < FlightTime - RaidEndBeforeArrival ? EnemyShipState.Present : EnemyShipState.Gone;
    }

    /// <summary>Seconds into the current cycle.</summary>
    public static TimeSpan Elapsed(DateTime utcNow)
        => TimeSpan.FromTicks(utcNow.Ticks % Cycle.Ticks);

    public static AirshipPhase PhaseAt(DateTime utcNow)
        => Elapsed(utcNow) < BoardingWindow ? AirshipPhase.Boarding : AirshipPhase.Flight;

    /// <summary>Whether the waiting room accepts passengers right now.</summary>
    public static bool IsBoarding(DateTime utcNow) => PhaseAt(utcNow) == AirshipPhase.Boarding;

    /// <summary>Time until the ship next leaves (0 while it is in the air).</summary>
    public static TimeSpan UntilDeparture(DateTime utcNow)
    {
        TimeSpan elapsed = Elapsed(utcNow);
        return elapsed < BoardingWindow ? BoardingWindow - elapsed : TimeSpan.Zero;
    }

    /// <summary>Time until the current flight lands (0 while docked).</summary>
    public static TimeSpan UntilArrival(DateTime utcNow)
    {
        TimeSpan elapsed = Elapsed(utcNow);
        return elapsed < BoardingWindow ? TimeSpan.Zero : Cycle - elapsed;
    }
}

/// <summary>
/// The tick that runs the ships: on the boarding→flight flip every passenger in a route's
/// waiting room is moved onto its flight map; on the flight→boarding flip everyone on the flight
/// map (and in its cabin) is moved to the arrival station. Passengers are moved through their
/// own session's warp delegate — the same path an NPC script's <c>player.warp</c> takes.
/// </summary>
public sealed class AirshipService
{
    /// <summary>
    /// レッサーバルログ (8150000, Lv100, flySpeed 10 — GMS "Crimson Balrog"): the flying raider that
    /// boards from the enemy ship. Not 9300210 — that is the 武陵道場 クリムゾンバルログ, a walking
    /// copy with a <c>revive</c> hook (the invisible 9300216 check mob) that the reference's
    /// Event_DojoAgent uses; it looked wrong on the deck and left dojo machinery behind.
    /// </summary>
    public const int RaiderMobId = 8150000;

    /// <summary>How far above the enemy ship's anchor the raiders appear (they fly down from it).</summary>
    public const int RaiderSpawnHeight = 50;

    /// <summary>Horizontal spacing between raiders along the enemy ship.</summary>
    public const int RaiderSpawnSpacing = 60;

    private readonly FieldRegistry _fields;
    private readonly ChannelPackets? _packets;
    private readonly TimeSpan _interval;
    private readonly Func<DateTime> _clock;
    private AirshipPhase? _lastPhase;
    private EnemyShipState? _lastEnemy;
    private readonly Dictionary<int, List<int>> _raiders = new(); // flight map -> raider object ids

    public AirshipService(FieldRegistry fields, ChannelPackets? packets = null, TimeSpan? interval = null, Func<DateTime>? clock = null)
    {
        _fields = fields;
        _packets = packets;
        _interval = interval ?? TimeSpan.FromSeconds(1);
        _clock = clock ?? (() => AirshipSchedule.Clock());
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await TickAsync(_clock()).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }

    /// <summary>One tick: acts only when the phase changed since the last tick. Returns how many
    /// passengers were moved.</summary>
    public async ValueTask<int> TickAsync(DateTime utcNow)
    {
        AirshipPhase phase = AirshipSchedule.PhaseAt(utcNow);
        EnemyShipState enemy = AirshipSchedule.EnemyShipAt(utcNow);
        if (_lastPhase is null)
        {
            _lastPhase = phase; // first tick: just learn where we are, never move anyone mid-cycle
            _lastEnemy = enemy;
            return 0;
        }

        int moved = 0;
        if (phase != _lastPhase)
        {
            _lastPhase = phase;
            foreach (AirshipRoute route in AirshipRoute.All)
            {
                moved += phase == AirshipPhase.Flight
                    ? await MoveAllAsync(route.WaitingRoomMapId, route.FlightMapId).ConfigureAwait(false)
                    : await MoveAllAsync(route.FlightMapId, route.ArrivalMapId).ConfigureAwait(false)
                      + await MoveAllAsync(route.CabinMapId, route.ArrivalMapId).ConfigureAwait(false);
            }
        }

        if (enemy != _lastEnemy)
        {
            _lastEnemy = enemy;
            foreach (AirshipRoute route in AirshipRoute.All)
            {
                if (enemy == EnemyShipState.Present)
                {
                    await RaidAsync(route.FlightMapId).ConfigureAwait(false);
                }
                else if (enemy == EnemyShipState.Gone)
                {
                    await EndRaidAsync(route.FlightMapId).ConfigureAwait(false);
                }
            }
        }

        return moved;
    }

    /// <summary>
    /// The Balrog ship pulls alongside: everyone aboard is told (see <see cref="AnnounceEnemyShipAsync"/>)
    /// and the raiders appear at the enemy ship (the map's <c>shipObj</c>, shipKind 1), flying down
    /// onto the deck. Nobody aboard → nothing to raid.
    /// </summary>
    private async ValueTask RaidAsync(int flightMapId)
    {
        Field field = _fields.Get(flightMapId);
        FieldPlayer? anchor = field.Players.FirstOrDefault();
        if (anchor is null || _packets is null)
        {
            return;
        }

        await AnnounceEnemyShipAsync(field, arriving: true).ConfigureAwait(false);

        MobData? stats = _fields.MobProvider?.GetMob(RaiderMobId);
        int count = Math.Clamp(Cronus.Common.GameConstants.AirshipBalrogCount, 1, 10);
        var ids = new List<int>();
        for (int i = 0; i < count; i++)
        {
            (int x, int y) = RaiderSpawnPoint(flightMapId, i, count, fallbackX: anchor.X, fallbackY: anchor.Y);
            FieldMob mob = field.SpawnMob(RaiderMobId, stats, (short)x, (short)y, foothold: 0);
            ids.Add(mob.ObjectId);
            await field.BroadcastAsync(_packets.MobEnterField(mob)).ConfigureAwait(false);
        }

        _raiders[flightMapId] = ids;
    }

    /// <summary>Where raider <paramref name="index"/> of <paramref name="count"/> appears: spread
    /// along the enemy ship above its anchor, or at the fallback when the map has no ship object.</summary>
    public (int X, int Y) RaiderSpawnPoint(int flightMapId, int index, int count, int fallbackX, int fallbackY)
    {
        ShipObjectData? ship = _fields.MapProvider?.GetMap(flightMapId)?.ShipObject;
        if (ship is null)
        {
            return (fallbackX, fallbackY);
        }

        int x = ship.X + (int)Math.Round((index - (count - 1) / 2.0) * RaiderSpawnSpacing);
        return (x, ship.Y - RaiderSpawnHeight);
    }

    /// <summary>
    /// Tells everyone on the flight map that the Balrog ship arrives / leaves. The oracle has no
    /// verified packet for this (its OnContiState replies CONTIMOVE(TARGET_MOVEFIELD, MOBGEN) on
    /// entry and nothing else); a live ride showed that CONTIMOVE(10, 4) alone draws no ship. So
    /// the client gets both candidates: LP_CONTISTATE(state, appearShip=1) — the packet the
    /// stations use for their docked ship, with the "CShip::AppearShip" flag set — and the
    /// oracle's CONTIMOVE. `/conti` sends either by hand for the live bisect; whichever the client
    /// honours becomes the only one sent.
    /// </summary>
    public async ValueTask AnnounceEnemyShipAsync(Field field, bool arriving)
    {
        if (_packets is null)
        {
            return;
        }

        byte state = arriving ? ChannelPackets.ContiMobGen : ChannelPackets.ContiMobDestroy;
        await field.BroadcastAsync(_packets.ContiState(state, appearShip: arriving ? (byte)1 : (byte)0)).ConfigureAwait(false);
        await field.BroadcastAsync(_packets.ContiMove(ChannelPackets.ContiTargetMoveField, state)).ConfigureAwait(false);
    }

    /// <summary>The Balrog ship peels away before landing: the client hides it (CONTI_MOBDESTROY)
    /// and any raider still standing leaves with it.</summary>
    private async ValueTask EndRaidAsync(int flightMapId)
    {
        Field field = _fields.Get(flightMapId);
        if (_packets is not null && field.Players.Count > 0)
        {
            await AnnounceEnemyShipAsync(field, arriving: false).ConfigureAwait(false);
        }

        if (_raiders.Remove(flightMapId, out List<int>? ids))
        {
            foreach (int oid in ids)
            {
                if (field.RemoveMob(oid) && _packets is not null)
                {
                    await field.BroadcastAsync(_packets.MobLeaveField(oid, deadType: 0)).ConfigureAwait(false);
                }
            }
        }
    }

    private async ValueTask<int> MoveAllAsync(int fromMapId, int toMapId)
    {
        int moved = 0;
        foreach (FieldPlayer passenger in _fields.Get(fromMapId).Players.ToList())
        {
            if (passenger.WarpAsync is { } warp)
            {
                try
                {
                    await warp(toMapId, 0).ConfigureAwait(false);
                    moved++;
                }
                catch (Exception)
                {
                    // a passenger whose session is going away — leave them; never stop the ship
                }
            }
        }

        return moved;
    }
}
