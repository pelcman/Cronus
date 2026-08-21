using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class BuddyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

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

    /// <summary>Adds "Bob" as a friend once in the field.</summary>
    private sealed class Asker : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opFriend = ServerOps.Get(ServerOpcode.FriendResult);
        private bool _sent;

        public Asker(int characterId) => _characterId = characterId;

        public TaskCompletionSource Added { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.FriendRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteByte(1);          // FriendReq_SetFriend
                w.WriteString("Bob");
                w.WriteString("");       // tag
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opFriend && p.ReadByte() == 10) // SetFriend_Done
            {
                Added.TrySetResult();
            }
        }
    }

    /// <summary>Waits for the invite popup and accepts it.</summary>
    private sealed class Acceptor : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opFriend = ServerOps.Get(ServerOpcode.FriendResult);
        private bool _accepted;

        public Acceptor(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Accepted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opFriend)
            {
                byte flag = p.ReadByte();
                if (flag == 9) // the invite popup -> accept
                {
                    int fromId = p.ReadInt();
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.FriendRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteByte(2); // FriendReq_AcceptFriend
                    w.WriteInt(fromId);
                    await session.SendAsync(w.ToArray());
                    _accepted = true;
                }
                else if (flag == 10 && _accepted) // our post-accept list
                {
                    Accepted.TrySetResult();
                }
            }
        }
    }

    [Fact]
    public async Task AddAndAccept_MakesMutualBuddies()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new Acceptor(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        var aliceClient = new Asker(alice.Id);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        await aliceClient.Added.Task.WaitAsync(cts.Token);
        await bobClient.Accepted.Task.WaitAsync(cts.Token);

        Assert.False(alice.Buddies[bob.Id].Hidden);  // Alice listed Bob from the start
        Assert.Equal("Bob", alice.Buddies[bob.Id].Name);
        Assert.False(bob.Buddies[alice.Id].Hidden);  // Bob's pending entry became visible on accept
        Assert.Equal("Alice", bob.Buddies[alice.Id].Name);
    }
}
