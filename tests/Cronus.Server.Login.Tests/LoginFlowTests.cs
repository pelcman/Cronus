using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Login;
using Xunit;

namespace Cronus.Server.Login.Tests;

/// <summary>
/// End-to-end login: a client session sends CP_CheckPassword through the real encrypted
/// wire, the LoginHandler authenticates, and the client decodes LP_CheckPasswordResult.
/// Exercises crypto + framing + opcode dispatch + the JMS v186 success layout together.
/// </summary>
public class LoginFlowTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class ClientHandler : PacketHandlerBase
    {
        private readonly string _id;
        private readonly string _password;
        private readonly TaskCompletionSource<(int Opcode, byte[] Body)> _received;

        public ClientHandler(string id, string password, TaskCompletionSource<(int, byte[])> received)
        {
            _id = id;
            _password = password;
            _received = received;
        }

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.CheckPassword), session.Config.PacketHeaderSize, session.Config.CodePage);
            w.WriteString(_id);
            w.WriteString(_password);
            w.WriteBytes(new byte[16]);  // machine id
            w.WriteInt(0);               // unk1
            w.WriteByte(0);              // unk2 (JMS >= 131)
            w.WriteByte(0);              // unk3 (JMS >= 147)
            await session.SendAsync(w.ToArray());
        }

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
        {
            _received.TrySetResult((opcode, packet.ReadRemaining()));
            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task CheckPassword_AutoRegister_ReturnsSuccessResult()
    {
        var config = ServerConfig.Jms186;
        var loginService = new LoginService(new InMemoryAccountRepository());

        var received = new TaskCompletionSource<(int Opcode, byte[] Body)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        await using var server = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server,
            new LoginHandler(ClientOps, ServerOps, loginService, config));
        await using var client = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client,
            new ClientHandler("newuser01", "pw", received));

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);

        (int Opcode, byte[] Body) result = await received.Task.WaitAsync(cts.Token);

        Assert.Equal(ServerOps.Get(ServerOpcode.CheckPasswordResult), result.Opcode);

        // Decode the JMS v186 success body.
        var reader = new PacketReader(PrependOpcode(result), config.CodePage);
        reader.ReadHeader(config.PacketHeaderSize);
        Assert.Equal(0, reader.ReadByte());        // result code = Success
        Assert.Equal(0, reader.ReadByte());        // OK
        int accountId = reader.ReadInt();
        Assert.True(accountId > 0);
        Assert.Equal(0, reader.ReadByte());        // gender
        Assert.Equal(0, reader.ReadByte());        // grade
        Assert.Equal(0, reader.ReadByte());        // grade (JMS >= 164)
        Assert.Equal("newuser01", reader.ReadString());
        Assert.Equal("newuser01", reader.ReadString());
    }

    private sealed class MapLoginClient : PacketHandlerBase
    {
        private readonly TaskCompletionSource<string> _mapName;

        public MapLoginClient(TaskCompletionSource<string> mapName) => _mapName = mapName;

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.JmsGetMapLogin), session.Config.PacketHeaderSize, session.Config.CodePage);
            await session.SendAsync(w.ToArray());
        }

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
        {
            if (opcode == ServerOps.Get(ServerOpcode.JmsSetMapLogin))
            {
                _mapName.TrySetResult(packet.ReadString());
            }

            return ValueTask.CompletedTask;
        }
    }

    [Fact]
    public async Task GetMapLogin_ReturnsLoginMapName()
    {
        var config = ServerConfig.Jms186;
        var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var server = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server,
            new LoginHandler(ClientOps, ServerOps, new LoginService(new InMemoryAccountRepository()), config));
        await using var client = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client,
            new MapLoginClient(received));

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);

        string mapName = await received.Task.WaitAsync(cts.Token);
        Assert.Equal("MapLogin", mapName);
    }

    [Fact]
    public async Task CheckPassword_WrongPassword_ReturnsFailureResult()
    {
        var config = ServerConfig.Jms186;
        var repo = new InMemoryAccountRepository();
        repo.Create("existing", "correct", gender: 0);
        var loginService = new LoginService(repo);

        var received = new TaskCompletionSource<(int Opcode, byte[] Body)>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        await using var server = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server,
            new LoginHandler(ClientOps, ServerOps, loginService, config));
        await using var client = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client,
            new ClientHandler("existing", "wrong", received));

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);

        (int Opcode, byte[] Body) result = await received.Task.WaitAsync(cts.Token);

        var reader = new PacketReader(PrependOpcode(result), config.CodePage);
        reader.ReadHeader(config.PacketHeaderSize);
        Assert.Equal((int)LoginResult.IncorrectPassword, reader.ReadByte());
    }

    // The dispatcher strips the opcode header before handing the reader to handlers; the test
    // re-attaches it so the whole packet can be decoded from the top.
    private static byte[] PrependOpcode((int Opcode, byte[] Body) result)
    {
        byte[] full = new byte[2 + result.Body.Length];
        full[0] = (byte)(result.Opcode & 0xFF);
        full[1] = (byte)((result.Opcode >> 8) & 0xFF);
        result.Body.CopyTo(full, 2);
        return full;
    }
}
