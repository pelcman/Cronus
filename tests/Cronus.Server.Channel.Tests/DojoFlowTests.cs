using System.IO.Pipelines;
using System.Text.RegularExpressions;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// The Mu Lung Dojo entry, through the real server: a level-30 character at So Gong's challenge
/// room (925020001) talks to 2091005, picks "challenge alone", and must land on a stage-1 fighting
/// map with the stage boss spawned and the countdown clock shown — the whole NPC → instance search
/// → warp → boss spawn → clock/effect path, over the wire, without losing the session. Skipped when
/// gamedata.db is not next to the repo.
/// </summary>
public class DojoFlowTests
{
    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private static readonly Regex MenuOption = new(@"#L(\d+)#", RegexOptions.Compiled);

    private sealed class DojoBot : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opScript = ServerOps.Get(ServerOpcode.ScriptMessage);
        private readonly int _opMob = ServerOps.Get(ServerOpcode.MobEnterField);
        private readonly int _opClock = ServerOps.Get(ServerOpcode.Clock);
        private readonly int _opFieldEffect = ServerOps.Get(ServerOpcode.FieldEffect);

        public DojoBot(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<int> Maps { get; } = new();
        public List<int> Mobs { get; } = new();
        public bool GotClockCountdown { get; private set; }
        public List<string> Screens { get; } = new();
        public System.Collections.Concurrent.BlockingCollection<(int Type, IReadOnlyList<int> Options)> Prompts { get; } = new();

        public override async ValueTask OnConnectedAsync(MapleSession session)
        {
            Session = session;
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.MigrateIn), session.Config.PacketHeaderSize, session.Config.CodePage);
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
                // LP_SetField: the enter-game blob starts [serverFlags:1][channel:4][... ][fieldKey],
                // then CharacterData. The map id is not at a fixed early offset here, so track order
                // by count and read the map another way: the first SetField is the entry map, the
                // next ones are warps. We learn the map from the field-clock/character instead — but
                // for this test we only need to know a warp happened, and check the boss + clock.
                Maps.Add(0);
                Entered.TrySetResult(true);
            }
            else if (opcode == _opScript)
            {
                p.ReadByte();
                p.ReadInt();
                int type = p.ReadByte();
                p.ReadByte();
                string text = p.ReadString();
                var options = type == 5
                    ? MenuOption.Matches(text).Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList()
                    : (IReadOnlyList<int>)Array.Empty<int>();
                Prompts.Add((type, options));
            }
            else if (opcode == _opMob)
            {
                p.ReadInt();       // object id
                p.ReadByte();      // control
                Mobs.Add(p.ReadInt()); // template id
            }
            else if (opcode == _opClock)
            {
                if (p.ReadByte() == 2)
                {
                    GotClockCountdown = true;
                }
            }
            else if (opcode == _opFieldEffect)
            {
                byte kind = p.ReadByte();
                if (kind == 3 || kind == 4)
                {
                    Screens.Add(p.ReadString());
                }
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask ChatAsync(string message)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), Session!.Config.PacketHeaderSize, Session.Config.CodePage);
            w.WriteInt(0);
            w.WriteString(message);
            w.WriteByte(0);
            await Session.SendAsync(w.ToArray());
        }

        public async ValueTask AnswerMenuAsync(int selection)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserScriptMessageAnswer), Session!.Config.PacketHeaderSize, Session.Config.CodePage);
            w.WriteByte(5);
            w.WriteByte(1);
            w.WriteInt(selection);
            await Session.SendAsync(w.ToArray());
        }
    }

    private static string? RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "gamedata.db")) && Directory.Exists(Path.Combine(dir.FullName, "scripts", "npc")))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    [Fact]
    public async Task ChallengeAlone_WarpsToStageOne_SpawnsTheBoss_ShowsTheClock()
    {
        string? root = RepoRoot();
        if (root is null)
        {
            return; // no client data here
        }

        var store = new SqliteWzStore(Path.Combine(root, "gamedata.db"));
        var maps = new WzMapProvider(store);
        var mobs = new WzMobProvider(store);
        var engine = new NpcScriptEngine(
            new FolderNpcScriptSource(Path.Combine(root, "scripts", "npc")),
            new FolderNpcScriptSource(Path.Combine(root, "scripts", "quest")),
            answerTimeoutMs: 10_000);

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Dojoer", MapId = MuLungDojo.Entrance, Level = 30, Job = 112, Meso = 1_000_000 });
        var fields = new FieldRegistry(maps, mobs);
        var bot = new DojoBot(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, engine);

        var c2s = new Pipe();
        var s2c = new Pipe();
        await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
        await using var client = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, bot);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        _ = server.RunAsync(cts.Token);
        _ = client.RunAsync(cts.Token);
        await bot.Entered.Task.WaitAsync(cts.Token);

        await bot.ChatAsync($"/talk {2091005}");
        Assert.True(bot.Prompts.TryTake(out (int Type, IReadOnlyList<int> Options) menu, TimeSpan.FromSeconds(5)), "So Gong never opened a menu");
        Assert.Equal(5, menu.Type);
        Assert.Contains(0, menu.Options); // "challenge alone"

        int mapsBefore = bot.Maps.Count;
        await bot.AnswerMenuAsync(0);

        // The warp to the stage map, the boss, and the clock all follow — give them a moment.
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline && (bot.Maps.Count <= mapsBefore || bot.Mobs.Count == 0 || !bot.GotClockCountdown))
        {
            await Task.Delay(50, cts.Token);
        }

        Assert.True(bot.Maps.Count > mapsBefore, "no warp into the dojo");
        Assert.Equal(MuLungDojo.SoloBase + 100, hero.MapId - (hero.MapId % 100)); // stage 1, any copy
        Assert.Contains(MuLungDojo.MobForStage(1), bot.Mobs);                       // マノ (9300184) spawned
        Assert.True(bot.GotClockCountdown, "the stage countdown clock was not shown");
        Assert.Contains(bot.Screens, s => s.StartsWith("dojang/start", StringComparison.Ordinal) || s == "Dojang/start");
    }
}
