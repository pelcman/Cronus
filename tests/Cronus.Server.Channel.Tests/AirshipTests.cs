using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// The airships: the station/flight-map handshake ported from ReqCField.OnContiState, the
/// wall-clock timetable, and the departure/arrival tick that moves passengers.
/// </summary>
public class AirshipTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    // ---- timetable -------------------------------------------------------------------------

    [Fact]
    public void Schedule_BoardsForTenMinutes_ThenFliesForFive()
    {
        DateTime cycleStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc); // ticks divisible by 15 min

        Assert.Equal(AirshipPhase.Boarding, AirshipSchedule.PhaseAt(cycleStart));
        Assert.Equal(AirshipPhase.Boarding, AirshipSchedule.PhaseAt(cycleStart.AddMinutes(9.99)));
        Assert.Equal(AirshipPhase.Flight, AirshipSchedule.PhaseAt(cycleStart.AddMinutes(10)));
        Assert.Equal(AirshipPhase.Flight, AirshipSchedule.PhaseAt(cycleStart.AddMinutes(14.99)));
        Assert.Equal(AirshipPhase.Boarding, AirshipSchedule.PhaseAt(cycleStart.AddMinutes(15)));

        Assert.Equal(TimeSpan.FromMinutes(7), AirshipSchedule.UntilDeparture(cycleStart.AddMinutes(3)));
        Assert.Equal(TimeSpan.Zero, AirshipSchedule.UntilDeparture(cycleStart.AddMinutes(12)));
        Assert.Equal(TimeSpan.FromMinutes(3), AirshipSchedule.UntilArrival(cycleStart.AddMinutes(12)));
    }

    // ---- departure / arrival tick ----------------------------------------------------------

    [Fact]
    public async Task Tick_MovesWaitingRoomToFlight_ThenFlightToArrival()
    {
        var fields = new FieldRegistry();
        AirshipRoute route = AirshipRoute.ElliniaToOrbis;
        var moves = new List<(string Who, int To)>();

        int nextId = 0;
        FieldPlayer Passenger(string name, int mapId)
        {
            // distinct ids — Field tracks players by Character.Id
            var p = new FieldPlayer(new Character { Id = ++nextId, Name = name, MapId = mapId }, null!);
            p.WarpAsync = (to, _) =>
            {
                moves.Add((name, to));
                fields.Get(mapId).Leave(p.Character.Id);
                return ValueTask.CompletedTask;
            };
            fields.Get(mapId).Enter(p);
            return p;
        }

        Passenger("A", route.WaitingRoomMapId);
        Passenger("B", route.WaitingRoomMapId);
        Passenger("Bystander", route.StationMapId); // not in the waiting room — never moved

        DateTime t0 = new(2026, 1, 1, 0, 5, 0, DateTimeKind.Utc); // boarding
        var svc = new AirshipService(fields, clock: () => t0);

        Assert.Equal(0, await svc.TickAsync(t0));                     // first tick only learns the phase
        Assert.Equal(0, await svc.TickAsync(t0.AddMinutes(1)));       // still boarding → nothing
        Assert.Equal(2, await svc.TickAsync(t0.AddMinutes(6)));       // 00:11 → flight: both board
        Assert.Equal(new[] { ("A", route.FlightMapId), ("B", route.FlightMapId) }, moves.ToArray());

        moves.Clear();
        Passenger("A", route.FlightMapId);
        Passenger("C", route.CabinMapId);
        Assert.Equal(2, await svc.TickAsync(t0.AddMinutes(11)));      // 00:16 → docked: both land
        Assert.All(moves, m => Assert.Equal(route.ArrivalMapId, m.To));
        Assert.Contains(("C", route.ArrivalMapId), moves);
    }

    // ---- CP_CONTISTATE handshake (oracle: ReqCField.OnContiState) --------------------------

    private sealed class ContiClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opContiState = ServerOps.Get("LP_CONTISTATE");
        private readonly int _opContiMove = ServerOps.Get("LP_CONTIMOVE");
        private bool _asked;

        public ContiClient(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(string Kind, byte A, byte B)> Reply { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Set once the CP_CONTISTATE ask has been sent.</summary>
        public TaskCompletionSource<bool> Asked { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_asked)
            {
                _asked = true;
                var w = new PacketWriter(ClientOps.Get("CP_CONTISTATE"), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);   // the map id the client believes (unused by the oracle too)
                w.WriteByte(0);
                await session.SendAsync(w.ToArray());
                Asked.TrySetResult(true);
            }
            else if (opcode == _opContiState)
            {
                Reply.TrySetResult(("state", p.ReadByte(), p.ReadByte()));
            }
            else if (opcode == _opContiMove)
            {
                Reply.TrySetResult(("move", p.ReadByte(), p.ReadByte()));
            }
        }
    }

    private static async Task<(string Kind, byte A, byte B)> AskAsync(int mapId)
        => (await AskOrSilenceAsync(mapId))!.Value;

    /// <summary>Asks CP_CONTISTATE on <paramref name="mapId"/>; null when the server stays silent.</summary>
    private static async Task<(string Kind, byte A, byte B)?> AskOrSilenceAsync(int mapId)
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Rider", MapId = mapId });
        var map = new MapData { MapId = mapId, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);
        var client = new ContiClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);
        await client.Asked.Task.WaitAsync(cts.Token);
        Task done = await Task.WhenAny(client.Reply.Task, Task.Delay(400, cts.Token));
        return done == client.Reply.Task ? await client.Reply.Task : null;
    }

    [Fact]
    public async Task ContiState_AtAStation_AnswersDocked()
    {
        (string kind, byte state, byte appear) = await AskAsync(101000300); // Ellinia station
        Assert.Equal("state", kind);
        Assert.Equal(ChannelPackets.ContiWait, state);
        Assert.Equal(0, appear);
    }

    private static readonly DateTime CycleStart = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static async Task<T> WithClockAsync<T>(DateTime moment, Func<Task<T>> body)
    {
        Func<DateTime> old = AirshipSchedule.Clock;
        AirshipSchedule.Clock = () => moment;
        try { return await body(); }
        finally { AirshipSchedule.Clock = old; }
    }

    [Fact]
    public async Task ContiState_OnTheFlightMap_BeforeTheRaid_StaysSilent()
    {
        // 00:10:30 — thirty seconds into the flight, calm skies: like the oracle for any map it
        // has no state for, nothing is sent (a CONTIMOVE "moving" reply drew nothing live).
        (string, byte, byte)? reply = await WithClockAsync(CycleStart.AddMinutes(10).AddSeconds(30), () => AskOrSilenceAsync(200090010));
        Assert.Null(reply);
    }

    [Fact]
    public async Task ContiState_OnTheFlightMap_DuringTheRaid_AnswersMobGen()
    {
        // 00:12:00 — two minutes in, the enemy ship is alongside: a late joiner gets the oracle's
        // reply, CONTIMOVE(TARGET_MOVEFIELD, MOBGEN) — the same packet the raid broadcasts.
        (string kind, byte first, byte second) = await WithClockAsync(CycleStart.AddMinutes(12), () => AskAsync(200090010));
        Assert.Equal("move", kind);
        Assert.Equal(ChannelPackets.ContiTargetMoveField, first);
        Assert.Equal(ChannelPackets.ContiMobGen, second);
    }

    // ---- the raid timeline and the service that runs it ---------------------------------

    [Fact]
    public void EnemyShip_ArrivesAMinuteIn_AndLeavesBeforeLanding()
    {
        Assert.Equal(EnemyShipState.None, AirshipSchedule.EnemyShipAt(CycleStart.AddMinutes(5)));                 // boarding
        Assert.Equal(EnemyShipState.None, AirshipSchedule.EnemyShipAt(CycleStart.AddMinutes(10).AddSeconds(59)));  // calm skies
        Assert.Equal(EnemyShipState.Present, AirshipSchedule.EnemyShipAt(CycleStart.AddMinutes(11)));              // +60s: raid
        Assert.Equal(EnemyShipState.Present, AirshipSchedule.EnemyShipAt(CycleStart.AddMinutes(14).AddSeconds(29)));
        Assert.Equal(EnemyShipState.Gone, AirshipSchedule.EnemyShipAt(CycleStart.AddMinutes(14).AddSeconds(30)));  // 30s before landing
        Assert.Equal(EnemyShipState.None, AirshipSchedule.EnemyShipAt(CycleStart.AddMinutes(15)));                 // docked again
    }

    [Fact]
    public async Task Raid_SpawnsBalrogsAtTheEnemyShip_AndRemovesThemWhenItLeaves()
    {
        var mobs = new InMemoryMobProvider(new[] { new MobData { TemplateId = AirshipService.RaiderMobId, MaxHp = 60000 } });
        AirshipRoute route = AirshipRoute.ElliniaToOrbis;
        var maps = new InMemoryMapProvider(new[]
        {
            // The real 200090010 ship object: the Balrog ship sits at (485, -221).
            new MapData { MapId = route.FlightMapId, Portals = Array.Empty<PortalData>(), ShipObject = new ShipObjectData(485, -221, 1) },
        });
        var fields = new FieldRegistry(maps, mobs);
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        var passenger = new FieldPlayer(new Character { Id = 1, Name = "Rider", MapId = route.FlightMapId }, null!) { X = 100, Y = -50 };
        fields.Get(route.FlightMapId).Enter(passenger);

        DateTime t0 = CycleStart.AddMinutes(10).AddSeconds(10); // in flight, before the raid
        var svc = new AirshipService(fields, packets, clock: () => t0);
        await svc.TickAsync(t0);                                             // learn the state

        await svc.TickAsync(CycleStart.AddMinutes(11).AddSeconds(1));       // the enemy ship arrives
        var raiders = fields.Get(route.FlightMapId).Mobs.Where(m => m.TemplateId == AirshipService.RaiderMobId).ToList();
        Assert.Equal(GameConstants.AirshipBalrogCount, raiders.Count);
        Assert.All(raiders, m => Assert.InRange((int)m.X, 485 - 2 * AirshipService.RaiderSpawnSpacing, 485 + 2 * AirshipService.RaiderSpawnSpacing));
        Assert.All(raiders, m => Assert.Equal(-221 - AirshipService.RaiderSpawnHeight, (int)m.Y)); // above the enemy ship, not at the passenger
        Assert.All(raiders, m => Assert.Equal(60000, m.MaxHp));
        Assert.Equal(raiders.Count, raiders.Select(m => (int)m.X).Distinct().Count());               // spread out, not stacked
        Assert.All(raiders, m => Assert.Equal(passenger.Character.Id, m.ControllerId));           // someone runs their AI

        await svc.TickAsync(CycleStart.AddMinutes(14).AddSeconds(31));      // it peels away
        Assert.DoesNotContain(fields.Get(route.FlightMapId).Mobs, m => m.TemplateId == AirshipService.RaiderMobId);
    }

    [Fact]
    public void RaiderSpawnPoint_FallsBackToThePassenger_WithoutAShipObject()
    {
        var svc = new AirshipService(new FieldRegistry());
        Assert.Equal((100, -50), svc.RaiderSpawnPoint(200090010, 0, 2, fallbackX: 100, fallbackY: -50));
    }
}
