using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class PartyTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static ChannelPackets Packets() => new(ServerOps, ServerConfig.Jms186);

    // ---- small encoder layouts ----

    [Fact]
    public void PartyCreateDone_HasIdAndNoDoorPlaceholder()
    {
        var r = new PacketReader(Packets().PartyCreateDone(42), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.PartyResult), r.ReadHeader());
        Assert.Equal(8, r.ReadByte());          // PartyRes_CreateNewParty_Done
        Assert.Equal(42, r.ReadInt());          // party id
        Assert.Equal(999999999, r.ReadInt());   // door placeholder
        Assert.Equal(999999999, r.ReadInt());
        Assert.Equal(0L, r.ReadLong());
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void PartyInvite_HasInviterNameLevelJob()
    {
        var r = new PacketReader(Packets().PartyInvite(7, "Alice", 30, 100), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(4, r.ReadByte());          // PartyReq_InviteParty
        Assert.Equal(7, r.ReadInt());           // party id
        Assert.Equal("Alice", r.ReadString());
        Assert.Equal(30, r.ReadInt());          // level
        Assert.Equal(100, r.ReadInt());         // job (JMS >= 186)
        Assert.Equal(0, r.ReadByte());          // auto-join
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void PartyInviteSent_IsJustTheName()
    {
        var r = new PacketReader(Packets().PartyInviteSent("Bob"), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(22, r.ReadByte());
        Assert.Equal("Bob", r.ReadString());
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void PartyChangeLeader_HasNewLeaderAndDcFlag()
    {
        var r = new PacketReader(Packets().PartyChangeLeader(9, byDisconnect: false), ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(31, r.ReadByte());
        Assert.Equal(9, r.ReadInt());
        Assert.False(r.ReadBool());
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void PartyDepart_Disband_IsLeaderIdTwice()
    {
        var slots = SixSlots();
        var r = new PacketReader(
            Packets().PartyDepart(3, targetId: 5, "Alice", PartyDepart.Disband, slots, leaderId: 5, forChannel: 1),
            ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(12, r.ReadByte());
        Assert.Equal(3, r.ReadInt());     // party id
        Assert.Equal(5, r.ReadInt());     // target id
        Assert.False(r.ReadBool());       // 0 = disband
        Assert.Equal(5, r.ReadInt());     // leader id again
        Assert.Equal(0, r.Remaining);     // disband carries no member block
    }

    [Fact]
    public void PartyDepart_Expel_HasExpelFlagNameAndStatus()
    {
        var slots = SixSlots();
        var r = new PacketReader(
            Packets().PartyDepart(3, targetId: 7, "Bob", PartyDepart.Expel, slots, leaderId: 5, forChannel: 1),
            ServerConfig.Jms186.CodePage);
        r.ReadHeader();
        Assert.Equal(12, r.ReadByte());
        Assert.Equal(3, r.ReadInt());
        Assert.Equal(7, r.ReadInt());
        Assert.True(r.ReadBool());        // 1 = a member left
        Assert.True(r.ReadBool());        // 1 = expelled (not voluntary)
        Assert.Equal("Bob", r.ReadString());
        // member block follows (checked structurally elsewhere); just confirm it's non-empty
        Assert.True(r.Remaining > 0);
    }

    // ---- the byte-critical member-status block ----

    private static List<PartyMemberView> SixSlots() => new()
    {
        new PartyMemberView(5, "Alice", 100, 30, 100000000, 1, Online: true),
        new PartyMemberView(7, "Bob", 200, 25, 100000000, 1, Online: true),
        default, default, default, default,
    };

    [Fact]
    public void PartyRefresh_MemberBlock_HasExactStructure()
    {
        List<PartyMemberView> slots = SixSlots();
        byte[] bytes = Packets().PartyRefresh(11, slots, leaderId: 5, forChannel: 1, loading: false);

        var r = new PacketReader(bytes, ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.PartyResult), r.ReadHeader());
        Assert.Equal(7, r.ReadByte());   // PartyRes_LoadParty_Done / silent update
        Assert.Equal(11, r.ReadInt());   // party id

        // 6x id
        Assert.Equal(new[] { 5, 7, 0, 0, 0, 0 }, ReadInts(r, 6));
        // 6x name (13-byte fixed)
        Assert.Equal(new[] { "Alice", "Bob", "", "", "", "" }, ReadFixed(r, 6, 13));
        // 6x job
        Assert.Equal(new[] { 100, 200, 0, 0, 0, 0 }, ReadInts(r, 6));
        // 6x level
        Assert.Equal(new[] { 30, 25, 0, 0, 0, 0 }, ReadInts(r, 6));
        // 6x wire channel: online -> channel-1 (0); empty -> -2
        Assert.Equal(new[] { 0, 0, -2, -2, -2, -2 }, ReadInts(r, 6));
        // leader id
        Assert.Equal(5, r.ReadInt());
        // 6x map id: same channel -> map; empty (channel 0 != forChannel 1) -> 0
        Assert.Equal(new[] { 100000000, 100000000, 0, 0, 0, 0 }, ReadInts(r, 6));

        // 6x door block. Real members (channel==forChannel, !leaving) -> 5x int door.
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(999999999, r.ReadInt()); // door town
            Assert.Equal(999999999, r.ReadInt()); // door target
            Assert.Equal(0, r.ReadInt());          // door skill
            Assert.Equal(0, r.ReadInt());          // door x
            Assert.Equal(0, r.ReadInt());          // door y
        }
        // Empty members (other channel) -> int + long + long, all zero (not leaving).
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(0, r.ReadInt());
            Assert.Equal(0L, r.ReadLong());
            Assert.Equal(0L, r.ReadLong());
        }

        Assert.Equal(0, r.Remaining); // exact byte match, nothing left over
    }

    private static int[] ReadInts(PacketReader r, int n)
    {
        var a = new int[n];
        for (int i = 0; i < n; i++)
        {
            a[i] = r.ReadInt();
        }

        return a;
    }

    private static string[] ReadFixed(PacketReader r, int n, int size)
    {
        var a = new string[n];
        for (int i = 0; i < n; i++)
        {
            a[i] = r.ReadFixedString(size);
        }

        return a;
    }

    // ---- party model ----

    private static FieldPlayer Player(int id, string name, int level = 30)
    {
        var c = new Character { Id = id, AccountId = id, WorldId = 0, Name = name, Level = (byte)level };
        return new FieldPlayer(c, session: null!);
    }

    [Fact]
    public void Party_Create_LeaderIsFirstMember()
    {
        var leader = Player(1, "Lead");
        var party = new Party(100, leader);

        Assert.Equal(100, party.Id);
        Assert.Equal(1, party.LeaderId);
        Assert.True(party.IsLeader(1));
        Assert.Equal(1, party.Count);
        Assert.True(party.Contains(1));
    }

    [Fact]
    public void Party_OfflineMember_SurvivesAndReattaches()
    {
        var leader = Player(1, "Lead");
        var party = new Party(1, leader);
        party.TryAdd(Player(2, "B"));

        // The leader drops: still on the roster, hidden from fan-out, shown offline.
        Assert.True(party.MarkOffline(1));
        Assert.False(party.MarkOffline(3)); // not a member
        Assert.Equal(2, party.Count);                       // roster keeps them
        Assert.Single(party.Members);                       // fan-out skips them
        Assert.False(party.AllOffline);
        List<PartyMemberView> slots = party.ViewSlots();
        Assert.False(slots[0].Online);                      // leader greyed out
        Assert.True(slots[1].Online);

        // They come back (a new presence, e.g. on another channel).
        var returned = Player(1, "Lead");
        returned.Channel = 1;
        Assert.True(party.Reattach(returned));
        Assert.Equal(2, party.Members.Count);
        Assert.True(party.ViewSlots()[0].Online);
        Assert.Equal(2, party.ViewSlots()[0].Channel);      // 1-based channel

        // Everyone gone -> the party dissolves.
        Assert.True(party.MarkOffline(1));
        Assert.True(party.MarkOffline(2));
        Assert.True(party.AllOffline);
    }

    [Fact]
    public void Party_AddRemove_TracksMembership()
    {
        var party = new Party(1, Player(1, "A"));
        Assert.True(party.TryAdd(Player(2, "B")));
        Assert.False(party.TryAdd(Player(2, "B"))); // duplicate rejected
        Assert.Equal(2, party.Count);

        Assert.True(party.Remove(2));
        Assert.False(party.Contains(2));
        Assert.Equal(1, party.Count);
    }

    [Fact]
    public void Party_TryAdd_RejectsWhenFull()
    {
        var party = new Party(1, Player(1, "A"));
        for (int i = 2; i <= Party.Capacity; i++)
        {
            Assert.True(party.TryAdd(Player(i, "M" + i)));
        }

        Assert.True(party.IsFull);
        Assert.False(party.TryAdd(Player(99, "Late")));
    }

    [Fact]
    public void Party_SetLeader_OnlyForMembers()
    {
        var party = new Party(1, Player(1, "A"));
        party.TryAdd(Player(2, "B"));

        Assert.True(party.SetLeader(2));
        Assert.Equal(2, party.LeaderId);
        Assert.False(party.SetLeader(999)); // not a member
        Assert.Equal(2, party.LeaderId);
    }

    [Fact]
    public void PartyRegistry_Create_BindsLeader()
    {
        var reg = new PartyRegistry();
        var leader = Player(1, "A");
        Party party = reg.Create(leader);

        Assert.Same(party, reg.GetForCharacter(1));
        Assert.Same(party, reg.GetById(party.Id));
    }

    [Fact]
    public void PartyRegistry_Disband_UnbindsEveryone()
    {
        var reg = new PartyRegistry();
        Party party = reg.Create(Player(1, "A"));
        var b = Player(2, "B");
        party.TryAdd(b);
        reg.Register(2, party);

        reg.Disband(party);

        Assert.Null(reg.GetForCharacter(1));
        Assert.Null(reg.GetForCharacter(2));
        Assert.Null(reg.GetById(party.Id));
    }

    // ---- end-to-end: create -> invite -> join -> leave ----

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

    private static byte[] PartyRequest(MapleSession session, Action<PacketWriter> body)
    {
        var w = new PacketWriter(ClientOps.Get(ClientOpcode.PartyRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
        body(w);
        return w.ToArray();
    }

    /// <summary>Leader: creates a party, invites the joiner, records join + leave updates.</summary>
    private sealed class Leader : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _inviteeName;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opParty = ServerOps.Get(ServerOpcode.PartyResult);
        private bool _invited;

        public Leader(int characterId, string inviteeName)
        {
            _characterId = characterId;
            _inviteeName = inviteeName;
        }

        public TaskCompletionSource<string> JoinName { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> LeaveTargetId { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                await session.SendAsync(PartyRequest(session, w => w.WriteByte(1))); // create
            }
            else if (opcode == _opParty)
            {
                int op = p.ReadByte();
                if (op == 8 && !_invited) // CreateDone -> invite the joiner
                {
                    _invited = true;
                    await session.SendAsync(PartyRequest(session, w => { w.WriteByte(4); w.WriteString(_inviteeName); }));
                }
                else if (op == 15) // Join update: [partyId][joinerName][status]
                {
                    p.ReadInt();
                    JoinName.TrySetResult(p.ReadString());
                }
                else if (op == 12) // Depart: [partyId][targetId]...
                {
                    p.ReadInt();
                    LeaveTargetId.TrySetResult(p.ReadInt());
                }
            }
        }
    }

    /// <summary>Joiner: accepts the party invite, then leaves once the join is confirmed.</summary>
    private sealed class Joiner : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opParty = ServerOps.Get(ServerOpcode.PartyResult);
        private bool _left;

        public Joiner(int characterId) => _characterId = characterId;

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode != _opParty)
            {
                return;
            }

            int op = p.ReadByte();
            if (op == 4) // Invite popup: [partyId][inviterName][level][job][0] -> join
            {
                int partyId = p.ReadInt();
                await session.SendAsync(PartyRequest(session, w => { w.WriteByte(3); w.WriteInt(partyId); }));
            }
            else if (op == 15 && !_left) // join confirmed -> leave
            {
                _left = true;
                await session.SendAsync(PartyRequest(session, w => w.WriteByte(2))); // withdraw
            }
        }
    }

    [Fact]
    public async Task Party_Create_Invite_Join_Leave_Flow()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000, Level = 30 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000, Level = 25 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var parties = new PartyRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        // Bob online first so the invite finds him.
        var bobClient = new Joiner(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, channelId: 0, parties: parties);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);

        // Alice creates a party and invites Bob.
        var aliceClient = new Leader(alice.Id, "Bob");
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, channelId: 0, parties: parties);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        Assert.Equal("Bob", await aliceClient.JoinName.Task.WaitAsync(cts.Token));   // Bob joined
        Assert.Equal(bob.Id, await aliceClient.LeaveTargetId.Task.WaitAsync(cts.Token)); // then left
    }
}
