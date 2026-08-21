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

public class ReactorTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    /// <summary>A 3-hit box: 0→1→2, 2 terminal (like the 0002000 crate).</summary>
    private static ReactorData Box() => new()
    {
        Transitions = new Dictionary<int, int> { [0] = 1, [1] = 2 },
    };

    [Fact]
    public void ReactorData_StateMachine()
    {
        ReactorData box = Box();
        Assert.False(box.IsTerminal(0));
        Assert.Equal(1, box.NextState(0));
        Assert.Equal(2, box.NextState(1));
        Assert.True(box.IsTerminal(2));
    }

    [Fact]
    public void FieldReactor_BreakAndRespawn()
    {
        var reactor = new FieldReactor { ObjectId = 1, ReactorId = 1002008, ReactorTime = 3 };
        reactor.State = 2;
        reactor.Break(nowTick: 1000);

        Assert.True(reactor.IsDead);
        Assert.Equal(1000 + 3000, reactor.RespawnAtTick);

        reactor.Respawn();
        Assert.False(reactor.IsDead);
        Assert.Equal(0, reactor.State);
    }

    /// <summary>Hits the reactor until it breaks, tracking the state changes.</summary>
    private sealed class Smasher : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opEnter = ServerOps.Get(ServerOpcode.ReactorEnterField);
        private readonly int _opChange = ServerOps.Get(ServerOpcode.ReactorChangeState);
        private readonly int _opLeave = ServerOps.Get(ServerOpcode.ReactorLeaveField);
        private int _objectId;

        public Smasher(int characterId) => _characterId = characterId;

        public TaskCompletionSource<int> Broken { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<byte> StatesSeen { get; } = new();

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
            if (opcode == _opEnter)
            {
                _objectId = p.ReadInt();
                await Hit(session);
            }
            else if (opcode == _opChange)
            {
                p.ReadInt();
                StatesSeen.Add(p.ReadByte());
                await Hit(session); // keep smashing (harmless once broken)
            }
            else if (opcode == _opLeave)
            {
                Broken.TrySetResult(p.ReadInt());
            }
        }

        private async ValueTask Hit(MapleSession session)
        {
            if (Broken.Task.IsCompleted)
            {
                return; // the respawned reactor is left alone
            }

            var w = new PacketWriter(ClientOps.Get(ClientOpcode.ReactorHit), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(_objectId);
            w.WriteInt(0);   // character position flags
            w.WriteShort(0); // stance
            await session.SendAsync(w.ToArray());
        }
    }

    [Fact]
    public async Task HittingABox_BreaksItThroughItsStates()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hero", MapId = 100000000 });

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Reactors = new[] { new ReactorSpawn { ReactorId = 1002008, X = 100, Y = 50, ReactorTime = 3 } },
        };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var reactors = new InMemoryReactorProvider(new Dictionary<int, ReactorData> { [1002008] = Box() });

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Smasher(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, reactors: reactors);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        int brokenOid = await client.Broken.Task.WaitAsync(cts.Token);

        FieldReactor reactor = fields.Get(100000000).Reactors[0];
        Assert.Equal(reactor.ObjectId, brokenOid);
        Assert.True(reactor.IsDead);
        Assert.Contains((byte)1, client.StatesSeen); // the intermediate state was broadcast
        Assert.Contains((byte)2, client.StatesSeen); // and the final one before it vanished

        // The respawn sweep brings it back once the delay passes.
        var respawn = new MobRespawnService(fields, new ChannelPackets(ServerOps, ServerConfig.Jms186));
        await respawn.TickAsync(Environment.TickCount64 + 10_000);
        Assert.False(reactor.IsDead);
        Assert.Equal(0, reactor.State);
    }
}
