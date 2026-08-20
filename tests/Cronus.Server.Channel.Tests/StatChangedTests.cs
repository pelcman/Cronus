using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class StatChangedTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void StatChanged_Meso_HasExactLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var character = new Character { Name = "X", Meso = 12345 };

        byte[] bytes = packets.StatChanged(character, StatFlag.Meso);

        var reader = new PacketReader(bytes, ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.StatChanged), reader.ReadHeader());
        Assert.Equal(1, reader.ReadByte());              // unlock
        Assert.Equal((int)StatFlag.Meso, reader.ReadInt()); // statmask = 0x40000
        Assert.Equal(12345, reader.ReadInt());           // meso
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void BroadcastNotice_Layout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        byte[] notice = packets.BroadcastNotice("hello world");
        var reader = new PacketReader(notice, ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.BroadcastMsg), reader.ReadHeader());
        Assert.Equal(0, reader.ReadByte()); // BM_NOTICE
        Assert.Equal("hello world", reader.ReadString());
        Assert.Equal(0, reader.Remaining);

        byte[] alert = packets.BroadcastNotice("!", alert: true);
        var alertReader = new PacketReader(alert, ServerConfig.Jms186.CodePage);
        alertReader.ReadHeader();
        Assert.Equal(1, alertReader.ReadByte()); // BM_ALERT
    }

    [Fact]
    public void StatChanged_MultipleStats_WrittenInBitOrder()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var character = new Character { Name = "X", Level = 7, Hp = 250, Exp = 9000, Meso = 42 };

        byte[] bytes = packets.StatChanged(character, StatFlag.Level | StatFlag.Hp | StatFlag.Exp | StatFlag.Meso);

        var reader = new PacketReader(bytes, ServerConfig.Jms186.CodePage);
        reader.ReadHeader();
        reader.ReadByte(); // unlock
        int mask = reader.ReadInt();
        Assert.Equal((int)(StatFlag.Level | StatFlag.Hp | StatFlag.Exp | StatFlag.Meso), mask);
        // Ascending bit order: Level (0x10), Hp (0x400), Exp (0x10000), Meso (0x40000).
        Assert.Equal(7, reader.ReadByte());   // level
        Assert.Equal(250, reader.ReadShort()); // hp (16-bit)
        Assert.Equal(9000, reader.ReadInt());  // exp
        Assert.Equal(42, reader.ReadInt());    // meso
        Assert.Equal(0, reader.Remaining);
    }

    private sealed class MesoClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);

        public MesoClient(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public TaskCompletionSource<int> MesoUpdate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            var w = New(session, ClientOpcode.MigrateIn);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                var w = New(session, ClientOpcode.UserChat);
                w.WriteInt(0);
                w.WriteString("!meso 5000");
                w.WriteByte(0);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();               // unlock
                p.ReadInt();                // mask
                MesoUpdate.TrySetResult(p.ReadInt());
            }
        }

        private static PacketWriter New(MapleSession session, string opcodeName)
            => new(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);
    }

    [Fact]
    public async Task MesoCommand_UpdatesAndNotifiesClient()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Rich", MapId = 100000000, Meso = 100 });

        var client = new MesoClient(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = serverSession.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        int meso = await client.MesoUpdate.Task.WaitAsync(cts.Token);
        Assert.Equal(5100, meso);
        Assert.Equal(5100, hero.Meso);
    }
}
