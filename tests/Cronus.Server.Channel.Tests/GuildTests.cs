using System.IO.Pipelines;
using System.Text;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class GuildTests
{
    static GuildTests() => CodePage.Register(); // Shift-JIS for the manual byte readers

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    // ---- registry ----

    [Fact]
    public void Registry_InviteIsConsumedOnceAndGuildChecked()
    {
        var guilds = new GuildRegistry();
        guilds.Invite("Bob", guildId: 7);

        Assert.False(guilds.TakeInvite("Bob", 8)); // wrong guild
        Assert.True(guilds.TakeInvite("bob", 7));  // case-insensitive
        Assert.False(guilds.TakeInvite("Bob", 7)); // consumed
    }

    [Fact]
    public void Registry_CreateUsesReferenceDefaults()
    {
        var guilds = new GuildRegistry();
        GuildData g = guilds.Create("Cronus", leaderId: 42);

        Assert.True(g.Id > 0);
        Assert.Equal(42, g.LeaderId);
        Assert.Equal(new[] { "Master", "Jr. Master", "Member", "Member", "Member" }, g.RankTitles);
        Assert.Equal(10, g.Capacity);
        Assert.Same(g, guilds.Get(g.Id));
        Assert.Same(g, guilds.FindByName("cronus"));
    }

    // ---- packet layout (ports ResCWvsContext's guild builders, JMS v186) ----

    private sealed class Reader
    {
        private readonly byte[] _data;
        private int _pos;

        public Reader(byte[] data, int skip) { _data = data; _pos = skip; }

        public byte Byte() => _data[_pos++];
        public short Short() { short v = BitConverter.ToInt16(_data, _pos); _pos += 2; return v; }
        public int Int() { int v = BitConverter.ToInt32(_data, _pos); _pos += 4; return v; }
        public string Str()
        {
            int len = Short();
            string s = Encoding.GetEncoding(932).GetString(_data, _pos, len);
            _pos += len;
            return s;
        }

        public string Fixed(int len)
        {
            string s = Encoding.GetEncoding(932).GetString(_data, _pos, len).TrimEnd('\0');
            _pos += len;
            return s;
        }

        public int Remaining => _data.Length - _pos;
    }

    [Fact]
    public void GuildInfo_EncodesReferenceLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);
        var guild = new GuildData
        {
            Id = 3, Name = "Cronus", LeaderId = 1, Notice = "Welcome!", Gp = 100,
            LogoBG = 1001, LogoBGColor = 2, Logo = 4005, LogoColor = 3,
        };
        var members = new[]
        {
            new ChannelPackets.GuildMemberRow(1, "Alice", 110, 30, 1, true),
            new ChannelPackets.GuildMemberRow(2, "Bob", 0, 8, 5, false),
        };

        byte[] p = packets.GuildInfo(guild, members);
        var r = new Reader(p, skip: 2); // opcode

        Assert.Equal(26, r.Byte());        // showGuildInfo
        Assert.Equal(1, r.Byte());         // bInGuild
        Assert.Equal(3, r.Int());          // guild id
        Assert.Equal("Cronus", r.Str());
        Assert.Equal("Master", r.Str());   // five rank titles
        Assert.Equal("Jr. Master", r.Str());
        Assert.Equal("Member", r.Str());
        Assert.Equal("Member", r.Str());
        Assert.Equal("Member", r.Str());

        Assert.Equal(2, r.Byte());         // member count
        Assert.Equal(1, r.Int());          // ids first…
        Assert.Equal(2, r.Int());

        Assert.Equal("Alice", r.Fixed(13)); // …then the rows
        Assert.Equal(110, r.Int());        // job
        Assert.Equal(30, r.Int());         // level
        Assert.Equal(1, r.Int());          // rank
        Assert.Equal(1, r.Int());          // online
        Assert.Equal(0, r.Int());          // signature
        Assert.Equal(3, r.Int());          // alliance rank
        Assert.Equal("Bob", r.Fixed(13));
        Assert.Equal(0, r.Int());
        Assert.Equal(8, r.Int());
        Assert.Equal(5, r.Int());
        Assert.Equal(0, r.Int());          // offline
        Assert.Equal(0, r.Int());
        Assert.Equal(3, r.Int());

        Assert.Equal(10, r.Int());         // capacity
        Assert.Equal(1001, r.Short());     // logoBG
        Assert.Equal(2, r.Byte());
        Assert.Equal(4005, r.Short());     // logo
        Assert.Equal(3, r.Byte());
        Assert.Equal("Welcome!", r.Str()); // notice
        Assert.Equal(100, r.Int());        // GP
        Assert.Equal(0, r.Int());          // alliance id
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void GuildBuilders_EncodeReferenceOpsAndBodies()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        var invite = new Reader(packets.GuildInvite(3, "Alice", 30, 110), 2);
        Assert.Equal(5, invite.Byte());
        Assert.Equal(3, invite.Int());
        Assert.Equal("Alice", invite.Str());
        Assert.Equal(30, invite.Int());
        Assert.Equal(110, invite.Int());

        var left = new Reader(packets.GuildMemberLeft(3, 2, "Bob", expelled: false), 2);
        Assert.Equal(44, left.Byte());
        var expelled = new Reader(packets.GuildMemberLeft(3, 2, "Bob", expelled: true), 2);
        Assert.Equal(47, expelled.Byte());

        var disband = new Reader(packets.GuildDisband(3), 2);
        Assert.Equal(50, disband.Byte());
        Assert.Equal(3, disband.Int());
        Assert.Equal(1, disband.Byte());
        Assert.Equal(0, disband.Remaining);

        var online = new Reader(packets.GuildMemberOnline(3, 2, online: true), 2);
        Assert.Equal(61, online.Byte());
        Assert.Equal(3, online.Int());
        Assert.Equal(2, online.Int());
        Assert.Equal(1, online.Byte());

        var chat = new Reader(packets.GroupMessage(ChannelPackets.ChatGroupGuild, "Alice", "hi"), 2);
        Assert.Equal(2, chat.Byte());
        Assert.Equal("Alice", chat.Str());
        Assert.Equal("hi", chat.Str());
    }

    // ---- e2e: create by command, invite, accept, guild chat, disband on leader leave ----

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

    /// <summary>Creates a guild via /guildcreate on entry, then invites Bob once asked to.</summary>
    private sealed class Leader : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opGuild = ServerOps.Get(ServerOpcode.GuildResult);
        private bool _sent;

        public Leader(int characterId) => _characterId = characterId;

        public TaskCompletionSource InGuild { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource BobJoined { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);
                w.WriteString("/guildcreate Cronus");
                w.WriteBool(false);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opGuild)
            {
                byte op = p.ReadByte();
                if (op == 26) // showGuildInfo -> we're in; invite Bob
                {
                    InGuild.TrySetResult();
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.GuildRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteByte(0x05);
                    w.WriteString("Bob");
                    await session.SendAsync(w.ToArray());
                }
                else if (op == 39) // newGuildMember
                {
                    BobJoined.TrySetResult();
                }
            }
        }
    }

    /// <summary>Waits for the guild invite and accepts it, then says one guild-chat line.</summary>
    private sealed class Joiner : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opGuild = ServerOps.Get(ServerOpcode.GuildResult);
        private readonly int _opGroupMsg = ServerOps.Get(ServerOpcode.GroupMessage);

        public Joiner(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Joined { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Disbanded { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session)
            => await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opGuild)
            {
                byte op = p.ReadByte();
                if (op == 5) // invite popup -> accept
                {
                    int guildId = p.ReadInt();
                    var w = new PacketWriter(ClientOps.Get(ClientOpcode.GuildRequest), session.Config.PacketHeaderSize, session.Config.CodePage);
                    w.WriteByte(0x06);
                    w.WriteInt(guildId);
                    w.WriteInt(_characterId);
                    await session.SendAsync(w.ToArray());
                }
                else if (op == 26)
                {
                    Joined.TrySetResult();
                }
                else if (op == 50)
                {
                    Disbanded.TrySetResult();
                }
            }
        }
    }

    [Fact]
    public async Task CreateInviteJoin_ThenLeaderLeaveDisbands()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000, Level = 30 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var guilds = new GuildRegistry();

        using var cts = new CancellationTokenSource(Timeout);

        var bobClient = new Joiner(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, guilds: guilds);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        var aliceClient = new Leader(alice.Id);
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, guilds: guilds);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        await aliceClient.InGuild.Task.WaitAsync(cts.Token);
        Assert.Equal(1, alice.GuildRank);
        Assert.True(alice.GuildId > 0);

        await bobClient.Joined.Task.WaitAsync(cts.Token);
        await aliceClient.BobJoined.Task.WaitAsync(cts.Token);
        Assert.Equal(alice.GuildId, bob.GuildId);
        Assert.Equal(5, bob.GuildRank);

        // The leader leaves -> the guild disbands and Bob is told.
        int guildId = alice.GuildId;
        var leave = new PacketWriter(ClientOps.Get(ClientOpcode.GuildRequest), ServerConfig.Jms186.PacketHeaderSize, ServerConfig.Jms186.CodePage);
        leave.WriteByte(0x07);
        leave.WriteInt(alice.Id);
        leave.WriteString("Alice");
        await aClient.SendAsync(leave.ToArray());

        await bobClient.Disbanded.Task.WaitAsync(cts.Token);
        Assert.Equal(0, alice.GuildId);
        Assert.Equal(0, bob.GuildId);
        Assert.Null(guilds.Get(guildId));
    }
}
