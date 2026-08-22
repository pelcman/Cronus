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

    [Fact]
    public void SqlReactorDropProvider_ReadsOnlyTheReactordropsTuples()
    {
        // The dump holds many tables; only the reactordrops INSERT rows must be read, and the
        // 5-column tuple maps as (reactordropid, reactorid, itemid, chance, questid).
        const string sql = """
            INSERT INTO `drop_data` (`id`, `dropperid`, `itemid`, `minimum_quantity`, `maximum_quantity`, `questid`, `chance`) VALUES
            (1, 100100, 2000000, 1, 1, 0, 400);
            INSERT INTO `reactordrops` (`reactordropid`, `reactorid`, `itemid`, `chance`, `questid`) VALUES
            (1, 2001, 4031161, 1, 1008),
            (3, 2001, 2010009, 2, -1);
            INSERT INTO `shopitems` (`shopitemid`, `shopid`, `itemid`, `price`, `position`) VALUES
            (9, 9999, 2000000, 50, 1);
            """;

        SqlReactorDropProvider drops = SqlReactorDropProvider.Parse(sql);

        Assert.Equal(2, drops.GetDrops(2001).Count);
        Assert.Equal(new ReactorDropEntry(4031161, 1, 1008), drops.GetDrops(2001)[0]);
        Assert.Equal(new ReactorDropEntry(2010009, 2, -1), drops.GetDrops(2001)[1]);
        Assert.Empty(drops.GetDrops(100100)); // drop_data / shopitems rows must not leak in
        Assert.Empty(drops.GetDrops(9999));
    }

    /// <summary>Smashes the reactor and records every DropEnterField item id.</summary>
    private sealed class DropWatcher : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opEnter = ServerOps.Get(ServerOpcode.ReactorEnterField);
        private readonly int _opChange = ServerOps.Get(ServerOpcode.ReactorChangeState);
        private readonly int _opDrop = ServerOps.Get(ServerOpcode.DropEnterField);
        private int _objectId;

        public DropWatcher(int characterId) => _characterId = characterId;

        public TaskCompletionSource<List<int>> Drops { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly List<int> _itemIds = new();

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
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.ReactorHit), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(_objectId);
                w.WriteInt(0);
                w.WriteShort(0);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opChange)
            {
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.ReactorHit), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(_objectId);
                w.WriteInt(0);
                w.WriteShort(0);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opDrop)
            {
                p.ReadByte();               // enter type
                p.ReadInt();                // drop oid
                p.ReadByte();               // meso flag (0 = item)
                _itemIds.Add(p.ReadInt());  // item id
                if (_itemIds.Count == 2)
                {
                    Drops.TrySetResult(_itemIds);
                }
            }
        }
    }

    [Fact]
    public async Task BreakingAReactor_SpawnsItsTableDrops_QuestGated()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Smash", MapId = 100000000 });
        hero.StartedQuests[1008] = string.Empty; // the quest-gated row's quest is active
        repo.Save(hero);

        var map = new MapData
        {
            MapId = 100000000,
            Portals = Array.Empty<PortalData>(),
            Reactors = new[] { new ReactorSpawn { ReactorId = 2001, X = 100, Y = 50, ReactorTime = 3 } },
        };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var reactors = new InMemoryReactorProvider(new Dictionary<int, ReactorData> { [2001] = Box() });
        var reactorDrops = new InMemoryReactorDropProvider(new Dictionary<int, IReadOnlyList<ReactorDropEntry>>
        {
            [2001] = new[]
            {
                new ReactorDropEntry(4031161, 1, 1008),   // chance 1 = always; quest active -> drops
                new ReactorDropEntry(2000000, 1, -1),     // chance 1, no gate -> drops
                new ReactorDropEntry(4031162, 1, 9999),   // gated on a quest the breaker lacks
            },
        });

        using var cts = new CancellationTokenSource(Timeout);
        var client = new DropWatcher(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields,
            reactors: reactors, reactorDrops: reactorDrops);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        List<int> drops = await client.Drops.Task.WaitAsync(cts.Token);

        Assert.Equal(new[] { 4031161, 2000000 }, drops);   // gated-in + ungated, in table order
        Assert.DoesNotContain(4031162, drops);             // the missing quest's row stayed out
    }
}
