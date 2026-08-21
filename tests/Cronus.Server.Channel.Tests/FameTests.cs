using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class FameTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    // ---- encoder layouts ----

    [Fact]
    public void GivePopularitySuccess_HasNameDirectionFame()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.GivePopularitySuccess("Bob", isUp: true, targetFame: 12), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.GivePopularityResult), r.ReadHeader());
        Assert.Equal(0, r.ReadByte());   // Success
        Assert.Equal("Bob", r.ReadString());
        Assert.True(r.ReadBool());       // up
        Assert.Equal(12, r.ReadInt());   // target's new fame
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void GivePopularityNotify_HasGiverAndDirection()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.GivePopularityNotify("Alice", isUp: false), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(5, r.ReadByte());   // Notify
        Assert.Equal("Alice", r.ReadString());
        Assert.False(r.ReadBool());      // down
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void GivePopularityError_IsJustTheOp()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.GivePopularityError(ChannelPackets.FameErrLevelLow), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(2, r.ReadByte());   // LevelLow
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void IncPopMessage_IsTypeAndDelta()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.IncPopMessage(1), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.Message), r.ReadHeader());
        Assert.Equal(5, r.ReadByte());   // MS_IncPOPMessage
        Assert.Equal(1, r.ReadInt());    // +1 fame
        Assert.Equal(0, r.Remaining);
    }

    // ---- end-to-end ----

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

    /// <summary>Bob: waits in the field and records who famed him.</summary>
    private sealed class Ratee : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opFame = ServerOps.Get(ServerOpcode.GivePopularityResult);

        public Ratee(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(string Giver, bool Up)> Notified { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opFame && p.ReadByte() == 5) // Notify
            {
                string giver = p.ReadString();
                bool up = p.ReadBool();
                Notified.TrySetResult((giver, up));
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Alice: fames the target up, then tries again (to hit the once-per-session limit).</summary>
    private sealed class Rater : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _targetId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opFame = ServerOps.Get(ServerOpcode.GivePopularityResult);
        private bool _sentFirst;
        private bool _sentSecond;

        public Rater(int characterId, int targetId)
        {
            _characterId = characterId;
            _targetId = targetId;
        }

        public TaskCompletionSource<int> FirstFame { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> SecondOp { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_sentFirst)
            {
                _sentFirst = true;
                await Fame(session);
            }
            else if (opcode == _opFame)
            {
                int op = p.ReadByte();
                if (op == 0 && !_sentSecond) // Success -> record fame, then fame again
                {
                    _sentSecond = true;
                    p.ReadString();               // target name
                    p.ReadBool();                 // up
                    FirstFame.TrySetResult(p.ReadInt()); // target's new fame
                    await Fame(session);
                }
                else if (_sentSecond)
                {
                    SecondOp.TrySetResult(op);    // expected: AlreadyToday (3)
                }
            }
        }

        private async ValueTask Fame(MapleSession session)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserGivePopularityRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteInt(_targetId);
            w.WriteByte(1); // up
            await session.SendAsync(w.ToArray());
        }
    }

    [Fact]
    public async Task GiveFame_RaisesTargetFame_AndBlocksSecondTime()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000, Level = 20 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000, Fame = 0 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new Ratee(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        var aliceClient = new Rater(alice.Id, bob.Id);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        int newFame = await aliceClient.FirstFame.Task.WaitAsync(cts.Token);
        Assert.Equal(1, newFame);                  // Bob's fame went 0 -> 1
        Assert.Equal(1, bob.Fame);

        (string giver, bool up) = await bobClient.Notified.Task.WaitAsync(cts.Token);
        Assert.Equal("Alice", giver);
        Assert.True(up);

        int secondOp = await aliceClient.SecondOp.Task.WaitAsync(cts.Token);
        Assert.Equal(3, secondOp);                 // AlreadyDoneToday on the repeat
    }
}
