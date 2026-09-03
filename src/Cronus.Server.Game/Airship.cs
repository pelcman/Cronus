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
    private readonly FieldRegistry _fields;
    private readonly TimeSpan _interval;
    private readonly Func<DateTime> _clock;
    private AirshipPhase? _lastPhase;

    public AirshipService(FieldRegistry fields, TimeSpan? interval = null, Func<DateTime>? clock = null)
    {
        _fields = fields;
        _interval = interval ?? TimeSpan.FromSeconds(1);
        _clock = clock ?? (() => DateTime.UtcNow);
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
        if (_lastPhase is null)
        {
            _lastPhase = phase; // first tick: just learn where we are, never move anyone mid-cycle
            return 0;
        }

        if (phase == _lastPhase)
        {
            return 0;
        }

        _lastPhase = phase;
        int moved = 0;
        foreach (AirshipRoute route in AirshipRoute.All)
        {
            moved += phase == AirshipPhase.Flight
                ? await MoveAllAsync(route.WaitingRoomMapId, route.FlightMapId).ConfigureAwait(false)
                : await MoveAllAsync(route.FlightMapId, route.ArrivalMapId).ConfigureAwait(false)
                  + await MoveAllAsync(route.CabinMapId, route.ArrivalMapId).ConfigureAwait(false);
        }

        return moved;
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
