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
