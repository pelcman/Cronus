using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class CommandTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private const int BobMap = 200000000;

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static byte[] MigrateIn(MapleSession session, int characterId)
    {
        var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
        w.WriteInt(characterId);
        w.WriteBytes(new byte[16]);
        w.WriteShort(0);
        w.WriteByte(0);
        w.WriteLong(0);
        return w.ToArray();
    }

    /// <summary>Bob: sits in his own map and reports who enters it.</summary>
    private sealed class Resident : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opEnter = ServerOps.Get(ServerOpcode.UserEnterField);

        public Resident(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opEnter)
            {
                Entered.TrySetResult(p.ReadInt()); // entering character id
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Alice: warps to a named player with the !warp command once she's in a field.</summary>
    private sealed class Warper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _targetName;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private bool _warped;

        public Warper(int characterId, string targetName)
        {
            _characterId = characterId;
            _targetName = targetName;
        }

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_warped)
            {
                _warped = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);                     // timestamp
                w.WriteString("!warp " + _targetName);
                w.WriteBool(false);                // onlyBalloon
                await session.SendAsync(w.ToArray());
            }
        }
    }

    [Fact]
    public async Task WarpCommand_MovesCallerToNamedPlayersMap()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = BobMap });

        var maps = new[]
        {
            new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() },
            new MapData { MapId = BobMap, Portals = Array.Empty<PortalData>() },
        };
        var mapProvider = new InMemoryMapProvider(maps);
        var fields = new FieldRegistry(mapProvider);

        using var cts = new CancellationTokenSource(Timeout);

        // Bob is online in his own map.
        var bobClient = new Resident(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, mapProvider);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        // Alice warps to Bob.
        var aliceClient = new Warper(alice.Id, "Bob");
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, mapProvider);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        int enteredId = await bobClient.Entered.Task.WaitAsync(cts.Token);
        Assert.Equal(alice.Id, enteredId);    // Alice showed up in Bob's map
        Assert.Equal(BobMap, alice.MapId);     // and her map is now Bob's
    }

    /// <summary>Sends a chat command on entry and reads back a single-stat StatChanged.</summary>
    private sealed class Commander : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _command;
        private readonly int _statBit;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);
        private bool _sent;

        public Commander(int characterId, string command, int statBit)
        {
            _characterId = characterId;
            _command = command;
            _statBit = statBit;
        }

        public TaskCompletionSource<int> StatValue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);
                w.WriteString(_command);
                w.WriteBool(false);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();               // unlock
                int mask = p.ReadInt();
                if ((mask & _statBit) != 0)
                {
                    StatValue.TrySetResult(p.ReadShort()); // single-stat command -> the value follows
                }
            }
        }
    }

    [Fact]
    public async Task JobCommand_SetsJob()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Boss", MapId = 100000000, Job = 0 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Commander(hero.Id, "!job 100", statBit: 0x20); // StatFlag.Job
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        int job = await client.StatValue.Task.WaitAsync(cts.Token);
        Assert.Equal(100, job);
        Assert.Equal(100, hero.Job);
    }
}
