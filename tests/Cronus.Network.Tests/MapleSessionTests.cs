using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Cronus.Common;
using Cronus.Network;
using Cronus.Network.Packets;
using Xunit;

namespace Cronus.Network.Tests;

public class MapleSessionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class DelegateHandler : PacketHandlerBase
    {
        public Func<MapleSession, ValueTask>? Connected;
        public Func<MapleSession, int, PacketReader, ValueTask>? Packet;

        public override ValueTask OnConnectedAsync(MapleSession session)
            => Connected?.Invoke(session) ?? ValueTask.CompletedTask;

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
            => Packet?.Invoke(session, opcode, packet) ?? ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Handshake_ThenEncryptedRoundTrip_BothDirections()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var serverReceived = new TaskCompletionSource<(int Opcode, byte[] Body)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var clientReceived = new TaskCompletionSource<(int Opcode, byte[] Body)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var serverHandler = new DelegateHandler
        {
            Packet = async (session, opcode, packet) =>
            {
                serverReceived.TrySetResult((opcode, packet.ReadRemaining()));

                var reply = new PacketWriter(opcode: 0x0000); // LP_CheckPasswordResult
                reply.WriteInt(4321);
                await session.SendAsync(reply.ToArray());
            },
        };

        var clientHandler = new DelegateHandler
        {
            Connected = async session =>
            {
                var login = new PacketWriter(opcode: 0x0001); // CP_CheckPassword
                login.WriteInt(1234);
                await session.SendAsync(login.ToArray());
            },
            Packet = (session, opcode, packet) =>
            {
                clientReceived.TrySetResult((opcode, packet.ReadRemaining()));
                return ValueTask.CompletedTask;
            },
        };

        await using var server = new MapleSession(
            clientToServer.Reader, serverToClient.Writer,
            ServerConfig.Jms186, SessionRole.Server, serverHandler, randomByte: () => 0x2A);
        await using var client = new MapleSession(
            serverToClient.Reader, clientToServer.Writer,
            ServerConfig.Jms186, SessionRole.Client, clientHandler);

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);

        (int Opcode, byte[] Body) fromClient = await serverReceived.Task.WaitAsync(cts.Token);
        (int Opcode, byte[] Body) fromServer = await clientReceived.Task.WaitAsync(cts.Token);

        Assert.Equal(0x0001, fromClient.Opcode);
        Assert.Equal(1234, BitConverter.ToInt32(fromClient.Body));
        Assert.Equal(0x0000, fromServer.Opcode);
        Assert.Equal(4321, BitConverter.ToInt32(fromServer.Body));
    }

    [Fact]
    public async Task MultipleFramesInSequence_KeepIvInSync()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        const int count = 25;
        var received = new List<int>();
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var serverHandler = new DelegateHandler
        {
            Packet = (session, opcode, packet) =>
            {
                lock (received)
                {
                    received.Add(packet.ReadInt());
                    if (received.Count == count)
                    {
                        done.TrySetResult();
                    }
                }

                return ValueTask.CompletedTask;
            },
        };

        var clientHandler = new DelegateHandler
        {
            Connected = async session =>
            {
                for (int i = 0; i < count; i++)
                {
                    var w = new PacketWriter(opcode: 0x0020); // CP_UserMove (arbitrary)
                    w.WriteInt(i * 1000 + 7);
                    await session.SendAsync(w.ToArray());
                }
            },
        };

        await using var server = new MapleSession(
            clientToServer.Reader, serverToClient.Writer,
            ServerConfig.Jms186, SessionRole.Server, serverHandler);
        await using var client = new MapleSession(
            serverToClient.Reader, clientToServer.Writer,
            ServerConfig.Jms186, SessionRole.Client, clientHandler);

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);

        await done.Task.WaitAsync(cts.Token);

        Assert.Equal(count, received.Count);
        for (int i = 0; i < count; i++)
        {
            Assert.Equal(i * 1000 + 7, received[i]);
        }
    }

    [Fact]
    public async Task Server_SendsKeepAlivePings_OnInterval()
    {
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        byte[] keepAlive = { 0x0F, 0x00 }; // a 2-byte "AliveReq" body
        var pinged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var clientHandler = new DelegateHandler
        {
            Packet = (session, opcode, packet) =>
            {
                if (opcode == 0x000F)
                {
                    pinged.TrySetResult();
                }

                return ValueTask.CompletedTask;
            },
        };

        await using var server = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, ServerConfig.Jms186, SessionRole.Server,
            new DelegateHandler(), randomByte: () => 0x2A,
            keepAlive: keepAlive, keepAliveInterval: TimeSpan.FromMilliseconds(50));
        await using var client = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, ServerConfig.Jms186, SessionRole.Client, clientHandler);

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);

        await pinged.Task.WaitAsync(cts.Token); // a ping arrived within the timeout
    }

    [Fact]
    public async Task Listener_AcceptsRealSocket_AndRoundTrips()
    {
        var clientReceived = new TaskCompletionSource<(int Opcode, byte[] Body)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        // Server echoes the payload back under a fixed opcode.
        var listener = new MapleListener(
            new IPEndPoint(IPAddress.Loopback, 0),
            ServerConfig.Jms186,
            () => new DelegateHandler
            {
                Packet = async (session, opcode, packet) =>
                {
                    var reply = new PacketWriter(opcode: 0x0002);
                    reply.WriteInt(packet.ReadInt());
                    await session.SendAsync(reply.ToArray());
                },
            });

        using var cts = new CancellationTokenSource(Timeout);
        _ = listener.RunAsync(cts.Token);

        // Wait for the listener to bind.
        IPEndPoint? endpoint = null;
        for (int i = 0; i < 100 && endpoint is null; i++)
        {
            endpoint = listener.LocalEndpoint;
            if (endpoint is null)
            {
                await Task.Delay(10, cts.Token);
            }
        }

        Assert.NotNull(endpoint);

        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await socket.ConnectAsync(endpoint!, cts.Token);
        await using var stream = new NetworkStream(socket, ownsSocket: true);

        var clientHandler = new DelegateHandler
        {
            Connected = async session =>
            {
                var w = new PacketWriter(opcode: 0x0001);
                w.WriteInt(9999);
                await session.SendAsync(w.ToArray());
            },
            Packet = (session, opcode, packet) =>
            {
                clientReceived.TrySetResult((opcode, packet.ReadRemaining()));
                return ValueTask.CompletedTask;
            },
        };

        await using var client = new MapleSession(
            PipeReader.Create(stream), PipeWriter.Create(stream),
            ServerConfig.Jms186, SessionRole.Client, clientHandler);
        _ = client.RunAsync(cts.Token);

        (int Opcode, byte[] Body) reply = await clientReceived.Task.WaitAsync(cts.Token);

        Assert.Equal(0x0002, reply.Opcode);
        Assert.Equal(9999, BitConverter.ToInt32(reply.Body));

        await listener.DisposeAsync();
    }
}
