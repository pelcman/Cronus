using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// Two clients migrate into the same map through separate encrypted sessions sharing one
/// FieldRegistry, then interact: enter-field announcements both ways, movement relay, chat
/// broadcast, and leave-field on disconnect.
/// </summary>
public class FieldInteractionTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed class FieldClient : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opEnter = ServerOps.Get(ServerOpcode.UserEnterField);
        private readonly int _opLeave = ServerOps.Get(ServerOpcode.UserLeaveField);
        private readonly int _opChat = ServerOps.Get(ServerOpcode.UserChat);
        private readonly int _opMove = ServerOps.Get(ServerOpcode.UserMove);

        public FieldClient(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }

        public TaskCompletionSource<bool> EnteredGame { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(int Id, string Name)> SawPlayerEnter { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> SawPlayerLeave { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(int Id, string Message)> SawChat { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<(int Id, byte[] Path)> SawMove { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            var w = NewPacket(session, ClientOpcode.MigrateIn);
            w.WriteInt(_characterId);
            w.WriteBytes(new byte[16]);
            w.WriteShort(0);
            w.WriteByte(0);
            w.WriteLong(0);
            await session.SendAsync(w.ToArray());
        }

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                EnteredGame.TrySetResult(true);
            }
            else if (opcode == _opEnter)
            {
                int id = p.ReadInt();
                p.ReadByte();                    // level
                string name = p.ReadString();
                SawPlayerEnter.TrySetResult((id, name));
            }
            else if (opcode == _opLeave)
            {
                SawPlayerLeave.TrySetResult(p.ReadInt());
            }
            else if (opcode == _opChat)
            {
                int id = p.ReadInt();
                p.ReadByte();                    // gm flag
                SawChat.TrySetResult((id, p.ReadString()));
            }
            else if (opcode == _opMove)
            {
                int id = p.ReadInt();
                SawMove.TrySetResult((id, p.ReadRemaining()));
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask SendChatAsync(string message)
        {
            var w = NewPacket(Session!, ClientOpcode.UserChat);
            w.WriteInt(0);                       // timestamp
            w.WriteString(message);
            w.WriteByte(0);                      // only balloon
            await Session!.SendAsync(w.ToArray());
        }

        public async ValueTask SendMoveAsync(byte[] rawPath)
        {
            var w = NewPacket(Session!, ClientOpcode.UserMove);
            w.WriteInt(-1);                      // JMS v186 move prefix
            w.WriteInt(-1);
            w.WriteByte(0);
            w.WriteInt(-1);
            w.WriteInt(-1);
            w.WriteInt(0);
            w.WriteInt(0);
            w.WriteInt(0);                       // JMS >= 164 crc
            w.WriteBytes(rawPath);
            await Session!.SendAsync(w.ToArray());
        }

        private static PacketWriter NewPacket(MapleSession session, string opcodeName)
            => new(ClientOps.Get(opcodeName), session.Config.PacketHeaderSize, session.Config.CodePage);
    }

    private sealed class Rig : IAsyncDisposable
    {
        public required MapleSession ServerSession { get; init; }
        public required MapleSession ClientSession { get; init; }
        public required FieldClient Client { get; init; }
        public required ChannelHandler Handler { get; init; }

        public async ValueTask DisposeAsync()
        {
            await ServerSession.DisposeAsync();
            await ClientSession.DisposeAsync();
        }
    }

    private static Rig Connect(
        ICharacterRepository characters, FieldRegistry fields, int characterId, CancellationToken ct)
    {
        var config = ServerConfig.Jms186;
        var client = new FieldClient(characterId);
        var clientToServer = new Pipe();
        var serverToClient = new Pipe();

        var handler = new ChannelHandler(ClientOps, ServerOps, characters, config, fields);
        var serverSession = new MapleSession(
            clientToServer.Reader, serverToClient.Writer, config, SessionRole.Server, handler);
        var clientSession = new MapleSession(
            serverToClient.Reader, clientToServer.Writer, config, SessionRole.Client, client);

        _ = serverSession.RunAsync(ct);
        _ = clientSession.RunAsync(ct);

        return new Rig { ServerSession = serverSession, ClientSession = clientSession, Client = client, Handler = handler };
    }

    private static InMemoryCharacterRepository TwoCharacters(out Character alpha, out Character beta)
    {
        var repo = new InMemoryCharacterRepository();
        alpha = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alpha", MapId = 100000000 });
        beta = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Beta", MapId = 100000000 });
        return repo;
    }

    [Fact]
    public async Task TwoPlayers_SeeEachOtherEnterAndChat()
    {
        var repo = TwoCharacters(out Character alpha, out Character beta);
        var fields = new FieldRegistry();
        using var cts = new CancellationTokenSource(Timeout);

        await using Rig first = Connect(repo, fields, alpha.Id, cts.Token);
        await first.Client.EnteredGame.Task.WaitAsync(cts.Token);

        await using Rig second = Connect(repo, fields, beta.Id, cts.Token);
        await second.Client.EnteredGame.Task.WaitAsync(cts.Token);

        // The first player is told about the newcomer; the newcomer is told about the first.
        (int id, string name) = await first.Client.SawPlayerEnter.Task.WaitAsync(cts.Token);
        Assert.Equal(beta.Id, id);
        Assert.Equal("Beta", name);

        (int id2, string name2) = await second.Client.SawPlayerEnter.Task.WaitAsync(cts.Token);
        Assert.Equal(alpha.Id, id2);
        Assert.Equal("Alpha", name2);

        // Chat broadcasts to the whole field.
        await first.Client.SendChatAsync("hello beta");
        (int chatFrom, string message) = await second.Client.SawChat.Task.WaitAsync(cts.Token);
        Assert.Equal(alpha.Id, chatFrom);
        Assert.Equal("hello beta", message);
    }

    [Fact]
    public async Task Movement_RelaysRawPathToOthers_AndUpdatesPosition()
    {
        var repo = TwoCharacters(out Character alpha, out Character beta);
        var fields = new FieldRegistry();
        using var cts = new CancellationTokenSource(Timeout);

        await using Rig first = Connect(repo, fields, alpha.Id, cts.Token);
        await first.Client.EnteredGame.Task.WaitAsync(cts.Token);
        await using Rig second = Connect(repo, fields, beta.Id, cts.Token);
        await second.Client.EnteredGame.Task.WaitAsync(cts.Token);
        await first.Client.SawPlayerEnter.Task.WaitAsync(cts.Token);

        // CMovePath head is [startX:2][startY:2]; the rest is opaque and must relay verbatim.
        byte[] rawPath = { 0x64, 0x00, 0xC8, 0x00, 0xAA, 0xBB, 0xCC, 0xDD, 0x01 };
        await first.Client.SendMoveAsync(rawPath);

        (int mover, byte[] relayed) = await second.Client.SawMove.Task.WaitAsync(cts.Token);
        Assert.Equal(alpha.Id, mover);
        Assert.Equal(rawPath, relayed);

        // Server-side position tracked from the path origin (100, 200).
        Field field = fields.Get(100000000);
        FieldPlayer alphaInField = Assert.Single(field.Players, fp => fp.Character.Id == alpha.Id);
        Assert.Equal(100, alphaInField.X);
        Assert.Equal(200, alphaInField.Y);
    }

    [Fact]
    public async Task Disconnect_BroadcastsLeaveField()
    {
        var repo = TwoCharacters(out Character alpha, out Character beta);
        var fields = new FieldRegistry();
        using var cts = new CancellationTokenSource(Timeout);

        await using Rig first = Connect(repo, fields, alpha.Id, cts.Token);
        await first.Client.EnteredGame.Task.WaitAsync(cts.Token);
        Rig second = Connect(repo, fields, beta.Id, cts.Token);
        await second.Client.EnteredGame.Task.WaitAsync(cts.Token);
        await first.Client.SawPlayerEnter.Task.WaitAsync(cts.Token);

        // Drop the second client's connection.
        await second.DisposeAsync();

        int leftId = await first.Client.SawPlayerLeave.Task.WaitAsync(cts.Token);
        Assert.Equal(beta.Id, leftId);
        Assert.DoesNotContain(fields.Get(100000000).Players, fp => fp.Character.Id == beta.Id);
    }
}
