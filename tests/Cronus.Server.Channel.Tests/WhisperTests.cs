using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class WhisperTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    // ---- encoder layout ----

    [Fact]
    public void WhisperResult_HasSenderAckLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        byte[] bytes = packets.WhisperResult("Bob", delivered: true);

        var r = new PacketReader(bytes, ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.Whisper), r.ReadHeader());
        Assert.Equal(0x0A, r.ReadByte());  // WP_Result | WP_Whisper
        Assert.Equal("Bob", r.ReadString());
        Assert.True(r.ReadBool());         // delivered
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void WhisperReceive_HasRecipientLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        byte[] bytes = packets.WhisperReceive("Alice", senderChannel: 0, message: "hi there");

        var r = new PacketReader(bytes, ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.Whisper), r.ReadHeader());
        Assert.Equal(0x12, r.ReadByte());  // WP_Receive | WP_Whisper
        Assert.Equal("Alice", r.ReadString());
        Assert.Equal(0, r.ReadByte());     // sender channel (0-based)
        Assert.Equal(0, r.ReadByte());     // admin flag
        Assert.Equal("hi there", r.ReadString());
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void WhisperLocationResult_OnlineReportsMap_OfflineReportsNone()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        var online = new PacketReader(packets.WhisperLocationResult("Bob", 100000000, online: true), ServerConfig.Jms186.CodePage);
        online.ReadHeader();
        Assert.Equal(0x09, online.ReadByte());  // WP_Result | WP_Location
        Assert.Equal("Bob", online.ReadString());
        Assert.Equal(1, online.ReadByte());     // LR_GameSvr
        Assert.Equal(100000000, online.ReadInt());

        var offline = new PacketReader(packets.WhisperLocationResult("Ghost", 0, online: false), ServerConfig.Jms186.CodePage);
        offline.ReadHeader();
        Assert.Equal(0x09, offline.ReadByte());
        Assert.Equal("Ghost", offline.ReadString());
        Assert.Equal(0, offline.ReadByte());    // LR_None
        Assert.Equal(0, offline.ReadInt());
    }

    // ---- end-to-end routing ----

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

    /// <summary>Enters the field, then captures an incoming whisper (WP_Receive | WP_Whisper).</summary>
    private sealed class Recipient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opWhisper = ServerOps.Get(ServerOpcode.Whisper);

        public Recipient(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<(string Sender, int Channel, string Message)> Received { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opWhisper && p.ReadByte() == 0x12)
            {
                string sender = p.ReadString();
                int channel = p.ReadByte();
                p.ReadByte(); // admin
                string message = p.ReadString();
                Received.TrySetResult((sender, channel, message));
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Enters the field, whispers a target once, then captures the delivery ack.</summary>
    private sealed class Sender : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _targetName;
        private readonly string _message;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opWhisper = ServerOps.Get(ServerOpcode.Whisper);
        private bool _sent;

        public Sender(int characterId, string targetName, string message)
        {
            _characterId = characterId;
            _targetName = targetName;
            _message = message;
        }

        public TaskCompletionSource<(string Name, bool Delivered)> Ack { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.Whisper), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(0x06);        // WP_Whisper (0x02) | WP_Request (0x04)
                w.WriteString(_targetName);
                w.WriteString(_message);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opWhisper && p.ReadByte() == 0x0A)
            {
                string name = p.ReadString();
                bool delivered = p.ReadBool();
                Ack.TrySetResult((name, delivered));
            }
        }
    }

    [Fact]
    public async Task Whisper_DeliversToOnlineTarget_AndAcksSender()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        // Bob (recipient) comes online first.
        var bobClient = new Recipient(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        // Alice enters and whispers Bob.
        var aliceClient = new Sender(alice.Id, "Bob", "meet at henesys");
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        (string sender, int channel, string message) = await bobClient.Received.Task.WaitAsync(cts.Token);
        Assert.Equal("Alice", sender);
        Assert.Equal(0, channel);
        Assert.Equal("meet at henesys", message);

        (string name, bool delivered) = await aliceClient.Ack.Task.WaitAsync(cts.Token);
        Assert.Equal("Bob", name);
        Assert.True(delivered);
    }

    [Fact]
    public async Task Whisper_OfflineTarget_AcksNotDelivered()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var aliceClient = new Sender(alice.Id, "Nobody", "are you there?");
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        (string name, bool delivered) = await aliceClient.Ack.Task.WaitAsync(cts.Token);
        Assert.Equal("Nobody", name);
        Assert.False(delivered);
    }
}
