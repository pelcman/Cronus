using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class MessengerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    // ---- encoder layout ----

    [Fact]
    public void MessengerSelfEnterResult_IsOpAndSlot()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.MessengerSelfEnterResult(2), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.Messenger), r.ReadHeader());
        Assert.Equal(1, r.ReadByte());  // MSMP_SelfEnterResult
        Assert.Equal(2, r.ReadByte());  // slot
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void MessengerLeave_IsOpAndSlot()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.MessengerLeave(1), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(2, r.ReadByte());  // MSMP_Leave
        Assert.Equal(1, r.ReadByte());  // slot
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void MessengerInviteResult_HasNameAndFound()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.MessengerInviteResult("Bob", found: true), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(4, r.ReadByte());  // MSMP_InviteResult
        Assert.Equal("Bob", r.ReadString());
        Assert.True(r.ReadBool());
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void MessengerInvite_HasInviterChannelAndId()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.MessengerInvite("Alice", 0, 7777), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(3, r.ReadByte());  // MSMP_Invite
        Assert.Equal("Alice", r.ReadString());
        Assert.Equal(0, r.ReadByte());  // inviter channel
        Assert.Equal(7777, r.ReadInt());// messenger id
        Assert.Equal(0, r.ReadByte());
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void MessengerChat_IsOpAndMessage()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var r = new PacketReader(packets.MessengerChat("hello"), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(6, r.ReadByte());  // MSMP_Chat
        Assert.Equal("hello", r.ReadString());
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void MessengerEnter_HasSlotAvatarNameChannelIsNew()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var member = new Character { AccountId = 1, WorldId = 0, Name = "Zoe", Job = 100 };

        byte[] bytes = packets.MessengerEnter(1, member, channel: 0, isNew: true);

        // Compute the avatar-look length independently so the reader can skip past it (the
        // avatar-look encoding itself is verified via the field spawn packet tests).
        var lw = new PacketWriter(encoding: ServerConfig.Jms186.CodePage);
        Cronus.Server.Login.CharacterEncoder.WriteAvatarLook(lw, member);
        int avatarLen = lw.ToArray().Length;

        var r = new PacketReader(bytes, ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.Messenger), r.ReadHeader());
        Assert.Equal(0, r.ReadByte());  // MSMP_Enter
        Assert.Equal(1, r.ReadByte());  // slot
        r.Skip(avatarLen);              // avatar look
        Assert.Equal("Zoe", r.ReadString());
        Assert.Equal(0, r.ReadByte());  // channel
        Assert.True(r.ReadBool());      // isNew
        Assert.Equal(0, r.Remaining);
    }

    // ---- end-to-end: invite -> join -> chat -> leave ----

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

    private static byte[] Messenger(MapleSession session, Action<PacketWriter> body)
    {
        var w = new PacketWriter(ClientOps.Get(ClientOpcode.Messenger), session.Config.PacketHeaderSize, session.Config.CodePage);
        body(w);
        return w.ToArray();
    }

    /// <summary>Alice: creates a messenger, invites Bob, chats when Bob joins, records the leave.</summary>
    private sealed class Alice : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _inviteeName;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opMessenger = ServerOps.Get(ServerOpcode.Messenger);
        private bool _entered;

        public Alice(int characterId, string inviteeName)
        {
            _characterId = characterId;
            _inviteeName = inviteeName;
        }

        public TaskCompletionSource<bool> InviteFound { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> LeaveSlot { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                // Create a new messenger (id 0).
                await session.SendAsync(Messenger(session, w => { w.WriteByte(0); w.WriteInt(0); }));
            }
            else if (opcode == _opMessenger)
            {
                int op = p.ReadByte();
                if (op == 1 && !_entered) // MSMP_SelfEnterResult -> now invite Bob
                {
                    _entered = true;
                    await session.SendAsync(Messenger(session, w => { w.WriteByte(3); w.WriteString(_inviteeName); }));
                }
                else if (op == 0) // MSMP_Enter -> Bob joined, say hi
                {
                    await session.SendAsync(Messenger(session, w => { w.WriteByte(6); w.WriteString("hi bob"); }));
                }
                else if (op == 4) // MSMP_InviteResult
                {
                    p.ReadString();
                    InviteFound.TrySetResult(p.ReadBool());
                }
                else if (op == 2) // MSMP_Leave
                {
                    LeaveSlot.TrySetResult(p.ReadByte());
                }
            }
        }
    }

    /// <summary>Bob: accepts the invite, records the chat, then leaves.</summary>
    private sealed class Bob : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opMessenger = ServerOps.Get(ServerOpcode.Messenger);

        public Bob(int characterId) => _characterId = characterId;

        public TaskCompletionSource<string> SawChat { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode != _opMessenger)
            {
                return;
            }

            int op = p.ReadByte();
            if (op == 3) // MSMP_Invite -> [name][channel][id][0]; accept by entering that id
            {
                p.ReadString();
                p.ReadByte();
                int messengerId = p.ReadInt();
                await session.SendAsync(Messenger(session, w => { w.WriteByte(0); w.WriteInt(messengerId); }));
            }
            else if (op == 6) // MSMP_Chat -> record, then leave
            {
                string message = p.ReadString();
                SawChat.TrySetResult(message);
                await session.SendAsync(Messenger(session, w => w.WriteByte(2))); // MSMP_Leave
            }
        }
    }

    [Fact]
    public async Task Messenger_Invite_Join_Chat_Leave_Flow()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var messengers = new MessengerRegistry(new ChannelPackets(ServerOps, ServerConfig.Jms186));

        using var cts = new CancellationTokenSource(Timeout);

        // Bob online first so the invite finds him.
        var bobClient = new Bob(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, channelId: 0, messengers: messengers);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);

        // Alice enters, invites Bob, chats when he joins.
        var aliceClient = new Alice(alice.Id, "Bob");
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, channelId: 0, messengers: messengers);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        Assert.True(await aliceClient.InviteFound.Task.WaitAsync(cts.Token));      // Bob was invitable
        Assert.Equal("hi bob", await bobClient.SawChat.Task.WaitAsync(cts.Token)); // chat reached Bob
        Assert.Equal(1, await aliceClient.LeaveSlot.Task.WaitAsync(cts.Token));    // Bob left slot 1
    }

    [Fact]
    public async Task Messenger_InviteOfflineName_ReportsNotFound()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var messengers = new MessengerRegistry(new ChannelPackets(ServerOps, ServerConfig.Jms186));

        using var cts = new CancellationTokenSource(Timeout);

        var aliceClient = new Alice(alice.Id, "Ghost");
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, channelId: 0, messengers: messengers);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        Assert.False(await aliceClient.InviteFound.Task.WaitAsync(cts.Token));
    }
}
