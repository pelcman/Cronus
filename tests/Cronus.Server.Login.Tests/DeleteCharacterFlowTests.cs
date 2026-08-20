using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Login;
using Xunit;

namespace Cronus.Server.Login.Tests;

/// <summary>
/// End-to-end character deletion over the encrypted wire: log in, then (with a character already
/// present for the account) send CP_DeleteCharacter and decode LP_DeleteCharacterResult.
/// </summary>
public class DeleteCharacterFlowTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class DeleteClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opCheckPw = ServerOps.Get(ServerOpcode.CheckPasswordResult);
        private readonly int _opDelete = ServerOps.Get(ServerOpcode.DeleteCharacterResult);

        public DeleteClient(int characterId) => _characterId = characterId;

        public TaskCompletionSource<(int CharacterId, int Result)> Deleted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            var w = New(session, ClientOpcode.CheckPassword);
            w.WriteString("owner");
            w.WriteString("pw");
            w.WriteBytes(new byte[16]);
            w.WriteInt(0);
            w.WriteByte(0);
            w.WriteByte(0);
            await session.SendAsync(w.ToArray());
        }

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opCheckPw && p.ReadByte() == (int)LoginResult.Success)
            {
                var w = New(session, ClientOpcode.DeleteCharacter);
                w.WriteInt(_characterId);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opDelete)
            {
                int id = p.ReadInt();
                int result = p.ReadByte();
                Deleted.TrySetResult((id, result));
            }
        }

        private static PacketWriter New(MapleSession session, string opcodeName)
            => new(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);
    }

    [Fact]
    public async Task DeleteOwnedCharacter_Succeeds()
    {
        var config = ServerConfig.Jms186;
        var accounts = new InMemoryAccountRepository();
        Account owner = accounts.Create("owner", "pw", gender: 0);
        var characters = new InMemoryCharacterRepository();
        Character victim = characters.Create(new Character { AccountId = owner.Id, WorldId = 0, Name = "Victim" });

        var loginService = new LoginService(accounts);
        var client = new DeleteClient(victim.Id);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var server = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server,
            new LoginHandler(ClientOps, ServerOps, loginService, config, characters: characters));
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (int id, int result) = await client.Deleted.Task.WaitAsync(cts.Token);
        Assert.Equal(victim.Id, id);
        Assert.Equal((int)LoginResult.Success, result);
        Assert.Null(characters.Find(victim.Id));
    }

    [Fact]
    public async Task DeleteOtherAccountsCharacter_Fails()
    {
        var config = ServerConfig.Jms186;
        var accounts = new InMemoryAccountRepository();
        accounts.Create("owner", "pw", gender: 0);
        var characters = new InMemoryCharacterRepository();
        // A character owned by a DIFFERENT account (id 999).
        Character other = characters.Create(new Character { AccountId = 999, WorldId = 0, Name = "NotYours" });

        var loginService = new LoginService(accounts);
        var client = new DeleteClient(other.Id);

        var clientToServer = new Pipe();
        var serverToClient = new Pipe();
        await using var server = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server,
            new LoginHandler(ClientOps, ServerOps, loginService, config, characters: characters));
        await using var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client, client);

        using var cts = new CancellationTokenSource(Timeout);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        (int _, int result) = await client.Deleted.Task.WaitAsync(cts.Token);
        Assert.NotEqual((int)LoginResult.Success, result);
        Assert.NotNull(characters.Find(other.Id)); // not deleted
    }
}
