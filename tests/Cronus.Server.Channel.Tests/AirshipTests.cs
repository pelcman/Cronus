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
        return await client.Reply.Task.WaitAsync(cts.Token);
    }

    [Fact]
    public async Task ContiState_AtAStation_AnswersDocked()
    {
        (string kind, byte state, byte appear) = await AskAsync(101000300); // Ellinia station
        Assert.Equal("state", kind);
        Assert.Equal(ChannelPackets.ContiWait, state);
        Assert.Equal(0, appear);
    }

    [Fact]
    public async Task ContiState_OnTheFlightMap_AnswersMoveFieldWithMobGen()
    {
        (string kind, byte first, byte second) = await AskAsync(200090010); // riding to Orbis
        Assert.Equal("move", kind);
        Assert.Equal(ChannelPackets.ContiTargetMoveField, first);
        Assert.Equal(ChannelPackets.ContiMobGen, second);
    }
}
