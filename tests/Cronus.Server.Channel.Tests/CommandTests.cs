using System.IO.Pipelines;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class CommandTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
    private const int BobMap = 200000000;

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

    /// <summary>Bob: sits in his own map and reports who enters it.</summary>
    private sealed class Resident : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opEnter = ServerOps.Get(ServerOpcode.UserEnterField);

        public Resident(int characterId) => _characterId = characterId;

        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<int> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField)
            {
                Ready.TrySetResult();
            }
            else if (opcode == _opEnter)
            {
                Entered.TrySetResult(p.ReadInt()); // entering character id
            }

            return ValueTask.CompletedTask;
        }
    }

    /// <summary>Alice: warps to a named player with the !warp command once she's in a field.</summary>
    private sealed class Warper : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _targetName;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private bool _warped;

        public Warper(int characterId, string targetName)
        {
            _characterId = characterId;
            _targetName = targetName;
        }

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_warped)
            {
                _warped = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);                     // timestamp
                w.WriteString("/warp " + _targetName);
                w.WriteBool(false);                // onlyBalloon
                await session.SendAsync(w.ToArray());
            }
        }
    }

    [Fact]
    public async Task WarpCommand_MovesCallerToNamedPlayersMap()
    {
        var repo = new InMemoryCharacterRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = BobMap });

        var maps = new[]
        {
            new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() },
            new MapData { MapId = BobMap, Portals = Array.Empty<PortalData>() },
        };
        var mapProvider = new InMemoryMapProvider(maps);
        var fields = new FieldRegistry(mapProvider);

        using var cts = new CancellationTokenSource(Timeout);

        // Bob is online in his own map.
        var bobClient = new Resident(bob.Id);
        var bobHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, mapProvider);
        var b2s = new Pipe();
        var s2b = new Pipe();
        await using var bServer = new MapleSession(b2s.Reader, s2b.Writer, ServerConfig.Jms186, SessionRole.Server, bobHandler);
        await using var bClient = new MapleSession(s2b.Reader, b2s.Writer, ServerConfig.Jms186, SessionRole.Client, bobClient);
        _ = bServer.RunAsync(cts.Token);
        _ = bClient.RunAsync(cts.Token);
        await bobClient.Ready.Task.WaitAsync(cts.Token);

        // Alice warps to Bob.
        var aliceClient = new Warper(alice.Id, "Bob");
        var aliceHandler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, mapProvider);
        var a2s = new Pipe();
        var s2a = new Pipe();
        await using var aServer = new MapleSession(a2s.Reader, s2a.Writer, ServerConfig.Jms186, SessionRole.Server, aliceHandler);
        await using var aClient = new MapleSession(s2a.Reader, a2s.Writer, ServerConfig.Jms186, SessionRole.Client, aliceClient);
        _ = aServer.RunAsync(cts.Token);
        _ = aClient.RunAsync(cts.Token);

        int enteredId = await bobClient.Entered.Task.WaitAsync(cts.Token);
        Assert.Equal(alice.Id, enteredId);    // Alice showed up in Bob's map
        Assert.Equal(BobMap, alice.MapId);     // and her map is now Bob's
    }

    /// <summary>Sends a chat command on entry and reads back a single-stat StatChanged.</summary>
    private sealed class Commander : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _command;
        private readonly int _statBit;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opStat = ServerOps.Get(ServerOpcode.StatChanged);
        private bool _sent;

        public Commander(int characterId, string command, int statBit)
        {
            _characterId = characterId;
            _command = command;
            _statBit = statBit;
        }

        public TaskCompletionSource<int> StatValue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);
                w.WriteString(_command);
                w.WriteBool(false);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opStat)
            {
                p.ReadByte();               // unlock
                int mask = p.ReadInt();
                if ((mask & _statBit) != 0)
                {
                    StatValue.TrySetResult(p.ReadShort()); // single-stat command -> the value follows
                }
            }
        }
    }

    /// <summary>Sends a command on entry and counts skill-record updates.</summary>
    private sealed class SkillMaxer : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opSkill = ServerOps.Get(ServerOpcode.ChangeSkillRecordResult);
        private bool _sent;
        private int _seen;

        public SkillMaxer(int characterId) => _characterId = characterId;

        public TaskCompletionSource<int> ThreeSkills { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);
                w.WriteString("/maxskills");
                w.WriteBool(false);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opSkill && ++_seen == 3)
            {
                ThreeSkills.TrySetResult(_seen);
            }
        }
    }

    [Fact]
    public async Task MaxSkillsCommand_MaxesTheJobChain()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Vet", MapId = 100000000, Job = 111 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));
        var skills = new InMemorySkillProvider(maxLevels: new Dictionary<int, int>
        {
            [1001003] = 20,  // book 100 (1st job)
            [1101004] = 20,  // book 110 (2nd job)
            [1111002] = 30,  // book 111 (3rd job)
            [1121000] = 30,  // book 112 (4th job) — beyond job 111, must NOT be learned
        });

        using var cts = new CancellationTokenSource(Timeout);

        var client = new SkillMaxer(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, skills: skills);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.ThreeSkills.Task.WaitAsync(cts.Token);
        Assert.Equal(20, hero.Skills[1001003]);
        Assert.Equal(20, hero.Skills[1101004]);
        Assert.Equal(30, hero.Skills[1111002]);
        Assert.False(hero.Skills.ContainsKey(1121000)); // 4th-job book is out of reach at job 111
    }

    [Fact]
    public async Task JobCommand_SetsJob()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Boss", MapId = 100000000, Job = 0 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);

        var client = new Commander(hero.Id, "/job 100", statBit: 0x20); // StatFlag.Job
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        int job = await client.StatValue.Task.WaitAsync(cts.Token);
        Assert.Equal(100, job);
        Assert.Equal(100, hero.Job);
    }

    [Fact]
    public async Task StrCommand_OverwritesTheStat()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Mighty", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);
        var client = new Commander(hero.Id, "/str 999", statBit: 0x40); // StatFlag.Str
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        int str = await client.StatValue.Task.WaitAsync(cts.Token);
        Assert.Equal(999, str);
        Assert.Equal(999, hero.Str);
    }

    [Fact]
    public async Task LevelCommand_SetsLevelAndResetsExp()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Elder", MapId = 100000000, Exp = 42 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);
        var client = new Commander(hero.Id, "/level 50", statBit: 0x10); // StatFlag.Level (signal only)
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.StatValue.Task.WaitAsync(cts.Token); // the packet mixes Level+Exp; assert on the model
        Assert.Equal(50, hero.Level);
        Assert.Equal(0, hero.Exp);
    }

    [Fact]
    public async Task MaxHpCommand_ClampsCurrentHp()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Tank", MapId = 100000000 }); // 50/50 HP

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);
        var client = new Commander(hero.Id, "/maxhp 30", statBit: 0x800); // StatFlag.MaxHp (signal only)
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.StatValue.Task.WaitAsync(cts.Token); // Hp+MaxHp packet; assert on the model
        Assert.Equal(30, hero.MaxHp);
        Assert.Equal(30, hero.Hp); // current HP pulled down with the max
    }

    /// <summary>Sends one command on entry and collects every chat line the server replies with.</summary>
    private sealed class ChatCollector : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly string _command;
        private readonly int _expectedLines;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opChat = ServerOps.Get(ServerOpcode.UserChat);
        private bool _sent;

        public ChatCollector(int characterId, string command, int expectedLines)
        {
            _characterId = characterId;
            _command = command;
            _expectedLines = expectedLines;
        }

        public List<string> Lines { get; } = new();

        public TaskCompletionSource Enough { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask OnConnectedAsync(MapleSession session) =>
            await session.SendAsync(MigrateIn(session, _characterId));

        public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader p)
        {
            if (opcode == _opSetField && !_sent)
            {
                _sent = true;
                var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), session.Config.PacketHeaderSize, session.Config.CodePage);
                w.WriteInt(0);
                w.WriteString(_command);
                w.WriteBool(false);
                await session.SendAsync(w.ToArray());
            }
            else if (opcode == _opChat)
            {
                p.ReadInt();                 // character id
                p.ReadBool();                // isGm
                lock (Lines)
                {
                    Lines.Add(p.ReadString());
                    if (Lines.Count >= _expectedLines)
                    {
                        Enough.TrySetResult();
                    }
                }
            }
        }
    }

    /// <summary>Runs one chat command against a lone player and returns the reply lines.</summary>
    private static async Task<List<string>> ReplyLinesFor(
        string command, int expectedLines, Character hero, ICharacterRepository repo)
    {
        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);
        var client = new ChatCollector(hero.Id, command, expectedLines);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        await client.Enough.Task.WaitAsync(cts.Token);
        lock (client.Lines)
        {
            return client.Lines.ToList();
        }
    }

    [Fact]
    public async Task StatusCommand_SetsTheNamedStat()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Consol", MapId = 100000000 });

        var map = new MapData { MapId = 100000000, Portals = Array.Empty<PortalData>() };
        var fields = new FieldRegistry(new InMemoryMapProvider(new[] { map }));

        using var cts = new CancellationTokenSource(Timeout);
        var client = new Commander(hero.Id, "/status dex 321", statBit: 0x80); // StatFlag.Dex
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields);
        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, client);
        _ = server.RunAsync(cts.Token);
        _ = clientSession.RunAsync(cts.Token);

        Assert.Equal(321, await client.StatValue.Task.WaitAsync(cts.Token));
        Assert.Equal(321, hero.Dex);
    }

    [Fact]
    public async Task StatusCommand_WithNoArguments_PrintsTheStatSheet()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character
        {
            AccountId = 1, WorldId = 0, Name = "Sheet", MapId = 100000000, Level = 7, Str = 13,
        });

        List<string> lines = await ReplyLinesFor("/status", expectedLines: 5, hero, repo);

        Assert.Equal(5, lines.Count);                          // one chat packet per line
        Assert.Contains("Lv.7", lines[0]);
        Assert.Contains("STR 13", lines[2]);
        Assert.Contains("/status", lines[4]);                   // tells you how to change one
    }

    [Fact]
    public async Task WrongArguments_AnswerWithTheCommandsUsage()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Typo", MapId = 100000000 });

        // /item wants an id; a word is not one, so the guard rejects it.
        List<string> lines = await ReplyLinesFor("/item apple", expectedLines: 2, hero, repo);

        Assert.Contains("引数", lines[0]);
        Assert.Contains("/item <", lines[1]);   // the registered usage, verbatim
    }

    [Fact]
    public async Task LegacyStatSpelling_WithoutAValue_ShowsTheStatusUsage()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Legacy", MapId = 100000000 });

        // "/hp" still resolves (it is an alias), but with no value it explains the new spelling.
        List<string> lines = await ReplyLinesFor("/hp", expectedLines: 1, hero, repo);

        Assert.Equal("使い方: /status hp <値>", lines[0]);
    }

    [Fact]
    public async Task UnknownCommand_SuggestsTheClosestNameAndPointsAtHelp()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Lost", MapId = 100000000 });

        List<string> lines = await ReplyLinesFor("/healx", expectedLines: 2, hero, repo);

        Assert.Contains("/heal", lines[0]);     // did-you-mean
        Assert.Contains("/help", lines[1]);
    }

    [Fact]
    public async Task HelpCommand_IsBrokenIntoLines_AndCanDetailOneCommand()
    {
        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Reader", MapId = 100000000 });

        // The listing: a header, five category headings, one line per command, and a footer —
        // every one its own chat packet, which is what makes it readable in the client.
        List<string> listing = await ReplyLinesFor("/help", expectedLines: 12, hero, repo);
        Assert.Contains(listing, l => l.Contains("コマンド一覧"));
        Assert.Contains(listing, l => l.Contains("【移動】"));
        Assert.Contains(listing, l => l.Trim().StartsWith("/warp "));

        // The detail view for a single command, reachable by any of its aliases.
        List<string> detail = await ReplyLinesFor("/help map", expectedLines: 3, hero, repo);
        Assert.Contains("/warp", detail[0]);
        Assert.Contains("別名", detail[2]);
    }
}
