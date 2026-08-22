using System.IO.Pipelines;
using System.Net;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class ChannelTransferTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void MigrateCommand_HasExactVanillaLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        var r = new PacketReader(packets.MigrateCommand(IPAddress.Parse("203.0.113.9"), 7576), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.MigrateCommand), r.ReadHeader());
        Assert.Equal(1, r.ReadByte());
        Assert.Equal(203, r.ReadByte());
        Assert.Equal(0, r.ReadByte());
        Assert.Equal(113, r.ReadByte());
        Assert.Equal(9, r.ReadByte());
        Assert.Equal((short)7576, r.ReadShort());
        Assert.Equal(0, r.Remaining);
    }

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

    /// <summary>Migrates in and asks for channel 1; flags the migrate command (or the decline).</summary>
    private sealed class Switcher : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMigrate = ServerOps.Get(ServerOpcode.MigrateCommand);
        private readonly int _opIgnored = ServerOps.Get(ServerOpcode.TransferChannelReqIgnored);
        private bool _sent;

        public Switcher(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(byte[] Ip, short Port)> Migrated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Declined { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserTransferChannelRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(1); // to channel 1
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opMigrate)
            {
                p.ReadByte();
                Migrated.TrySetResult((p.ReadBytes(4), p.ReadShort()));
            }
            else if (opcode == _opIgnored)
            {
                Declined.TrySetResult();
            }
        }
    }

    [Fact]
    public async Task TransferChannel_SendsTargetEndpoint()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Hopper", MapId = 100000000, Hp = 50 });

        var endpoints = new[]
        {
            new IPEndPoint(IPAddress.Loopback, 7575),
            new IPEndPoint(IPAddress.Loopback, 7576),
        };
        var client = new Switcher(hero.Id);
        var handler = new ChannelHandler(
            ClientOps, ServerOps, repo, ServerConfig.Jms186, channelId: 0, channelEndpoints: endpoints);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var session = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = session.RunAsync(cts.Token);

        (byte[] ip, short port) = await client.Migrated.Task.WaitAsync(cts.Token);
        Assert.Equal(new byte[] { 127, 0, 0, 1 }, ip);
        Assert.Equal((short)7576, port);
    }

    /// <summary>Sits in the map and flags a received whisper.</summary>
    private sealed class Listener : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opWhisper = ServerOps.Get(ServerOpcode.Whisper);

        public Listener(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<(string From, int Channel, string Message)> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opWhisper)
            {
                int flags = p.ReadByte();
                if ((flags & 0x10) != 0) // WP_Receive
                {
                    string from = p.ReadString();
                    int channel = p.ReadByte();
                    p.ReadByte(); // admin flag
                    Received.TrySetResult((from, channel, p.ReadString()));
                }
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Migrates in, /finds then whispers a name; records both answers.</summary>
    private sealed class Caller : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _targetName;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opWhisper = ServerOps.Get(ServerOpcode.Whisper);
        private bool _sent;

        public Caller(int characterId, string targetName)
        {
            _characterId = characterId;
            _targetName = targetName;
        }

        public TaskCompletionSource<(byte LocationType, int Payload)> Location { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var find = new PacketWriter(ClientOps.Get(ClientOpcode.Whisper), session.Config.PacketHeaderSize, session.Config.CodePage);
                find.WriteByte(0x01 | 0x04); // WP_Location | WP_Request
                find.WriteString(_targetName);
                await session.SendAsync(find.ToArray());

                var whisper = new PacketWriter(ClientOps.Get(ClientOpcode.Whisper), session.Config.PacketHeaderSize, session.Config.CodePage);
                whisper.WriteByte(0x02 | 0x04); // WP_Whisper | WP_Request
                whisper.WriteString(_targetName);
                whisper.WriteString("hello across channels");
                await session.SendAsync(whisper.ToArray());
            }
            else if (opcode == _opWhisper)
            {
                int flags = p.ReadByte();
                if ((flags & 0x08) != 0 && (flags & 0x01) != 0) // WP_Result | WP_Location
                {
                    p.ReadString(); // target name
                    Location.TrySetResult((p.ReadByte(), p.ReadInt()));
                }
            }
        }
    }

    [Fact]
    public async Task WhisperAndFind_ReachAcrossChannels()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields0 = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var fields1 = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var world = new[] { fields0, fields1 };

        using var cts = new CancellationTokenSource(Timeout);

        // Bob sits on channel 1.
        var bobClient = new Listener(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields1, channelId: 1, worldFields: world);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        // Alice, on channel 0, finds and whispers him.
        var aliceClient = new Caller(alice.Id, "Bob");
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields0, channelId: 0, worldFields: world);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        (byte locationType, int payload) = await aliceClient.Location.Task.WaitAsync(cts.Token);
        Assert.Equal(3, locationType); // LR_OtherChannel
        Assert.Equal(2, payload);      // 1-based channel number

        (string from, int channel, string message) = await bobClient.Received.Task.WaitAsync(cts.Token);
        Assert.Equal("Alice", from);
        Assert.Equal(0, channel); // sender's wire channel id
        Assert.Equal("hello across channels", message);
    }

    [Fact]
    public async Task TransferChannel_SingleChannel_Declines()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Stuck", MapId = 100000000, Hp = 50 });

        var client = new Switcher(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186); // no endpoints

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var session = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = session.RunAsync(cts.Token);

        await client.Declined.Task.WaitAsync(cts.Token);
    }
}
