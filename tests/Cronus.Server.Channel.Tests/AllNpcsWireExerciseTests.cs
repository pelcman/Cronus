using System.IO.Pipelines;
using System.Text.RegularExpressions;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;
using Cronus.Server.Channel;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// The bot talks to every scripted NPC through the real server: real map data, real item and
/// quest data, the real script player — so a branch that warps, hands out an item, starts a quest
/// or opens a shop really does so to the test character, and every response goes over the wire.
/// Branches are explored per NPC (bounded) exactly like the headless exerciser; what this adds is
/// the server-side effects and their packets. It fails when the session dies (a server exception),
/// when a script throws ("[script]" on the console), or when a conversation never ends.
/// Skipped when gamedata.db is not next to the repo.
/// </summary>
public class AllNpcsWireExerciseTests
{
    private const int MaxRunsPerNpc = 6;
    private const int MaxPromptsPerRun = 30;
    private static readonly TimeSpan PromptWait = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(12);

    private static readonly Regex MenuOption = new(@"#L(\d+)#", RegexOptions.Compiled);

    private static OpcodeTable ClientOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ClientPacket.properties"));

    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    private sealed record Prompt(int Type, int NpcId, string Text, IReadOnlyList<int> Options);

    private sealed class TalkerBot : PacketHandlerBase
    {
        private readonly int _characterId;
        private readonly int _opSetField = ServerOps.Get(ServerOpcode.SetField);
        private readonly int _opScriptMessage = ServerOps.Get(ServerOpcode.ScriptMessage);
        private readonly int _opUserChat = ServerOps.Get(ServerOpcode.UserChat);
        private readonly System.Collections.Concurrent.BlockingCollection<Prompt> _prompts = new();

        public TalkerBot(int characterId) => _characterId = characterId;

        public MapleSession? Session { get; private set; }
        public TaskCompletionSource<bool> Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<string> Pong { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int MapChanges { get; private set; }

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
                MapChanges++;
                Entered.TrySetResult(true);
            }
            else if (opcode == _opScriptMessage)
            {
                p.ReadByte();                       // speaker type
                int npcId = p.ReadInt();
                int type = p.ReadByte();
                p.ReadByte();                       // param
                string text = p.ReadString();
                var options = type == 5
                    ? MenuOption.Matches(text).Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList()
                    : (IReadOnlyList<int>)Array.Empty<int>();
                _prompts.Add(new Prompt(type, npcId, text, options));
            }
            else if (opcode == _opUserChat)
            {
                p.ReadInt();
                p.ReadByte();
                string text = p.ReadString();
                if (text.StartsWith("pos:", StringComparison.Ordinal))
                {
                    Pong.TrySetResult(text);
                }
            }

            return ValueTask.CompletedTask;
        }

        public bool TryTakePrompt(TimeSpan wait, out Prompt? prompt) => _prompts.TryTake(out prompt, wait);

        public void DrainPrompts()
        {
            while (_prompts.TryTake(out _))
            {
            }
        }

        public async ValueTask ChatAsync(string message)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserChat), Session!.Config.PacketHeaderSize, Session.Config.CodePage);
            w.WriteInt(0);
            w.WriteString(message);
            w.WriteByte(0);
            await Session.SendAsync(w.ToArray());
        }

        /// <summary>CP_UserScriptMessageAnswer: [type][action][menu: int selection | text: string | avatar: byte index].</summary>
        public async ValueTask AnswerAsync(Prompt prompt, int action, int selection, string text)
        {
            var w = new PacketWriter(ClientOps.Get(ClientOpcode.UserScriptMessageAnswer), Session!.Config.PacketHeaderSize, Session.Config.CodePage);
            w.WriteByte((byte)prompt.Type);
            w.WriteByte((byte)action);
            switch (prompt.Type)
            {
                case 5: w.WriteInt(selection); break;
                case 3: w.WriteString(text); break;
                case 8: w.WriteByte((byte)Math.Max(0, selection)); break;
            }

            await Session.SendAsync(w.ToArray());
        }
    }

    private static (int Action, int Selection, string Text)[] Alternatives(Prompt p) => p.Type switch
    {
        2 or 13 => new[] { (1, -1, ""), (0, -1, "") },
        3 => new[] { (1, -1, "abc") },
        5 => p.Options.Count > 0 ? p.Options.Select(o => (1, o, "")).ToArray() : new[] { (1, 0, "") },
        8 => new[] { (1, 0, ""), (0, -1, "") },
        _ => new[] { (1, -1, "") },
    };

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
    public async Task TalkToEveryScriptedNpc_ThroughTheRealServer_EveryBranch_SessionSurvives()
    {
        string? root = RepoRoot();
        if (root is null)
        {
            return; // no client data here
        }

        var store = new SqliteWzStore(Path.Combine(root, "gamedata.db"));
        var maps = new WzMapProvider(store);
        var mobs = new WzMobProvider(store);
        var items = new WzItemProvider(store);
        var quests = new WzQuestProvider(store);
        var engine = new NpcScriptEngine(
            new FolderNpcScriptSource(Path.Combine(root, "scripts", "npc")),
            new FolderNpcScriptSource(Path.Combine(root, "scripts", "quest")),
            answerTimeoutMs: 10_000);
        List<int> npcIds = engine.ScriptedNpcIds.ToList();
        Assert.True(npcIds.Count > 100, $"only {npcIds.Count} scripted NPCs");

        var repo = new InMemoryCharacterRepository();
        Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Talker", MapId = 100000000, Level = 200, Job = 132, Meso = 500_000_000 });
        var fields = new FieldRegistry(maps, mobs);
        var bot = new TalkerBot(hero.Id);
        var handler = new ChannelHandler(ClientOps, ServerOps, repo, ServerConfig.Jms186, fields, maps, engine, items: items, quests: quests);

        // Script failures are logged as "[script] …" — capture the console for the assertion.
        TextWriter originalOut = Console.Out;
        var console = new StringWriter();
        Console.SetOut(console);
        try
        {
            var c2s = new Pipe();
            var s2c = new Pipe();
            await using var server = new MapleSession(c2s.Reader, s2c.Writer, ServerConfig.Jms186, SessionRole.Server, handler);
            await using var clientSession = new MapleSession(s2c.Reader, c2s.Writer, ServerConfig.Jms186, SessionRole.Client, bot);
            using var cts = new CancellationTokenSource(Timeout);
            _ = server.RunAsync(cts.Token);
            _ = clientSession.RunAsync(cts.Token);
            await bot.Entered.Task.WaitAsync(cts.Token);

            var problems = new List<string>();
            int conversations = 0;
            int prompts = 0;
            foreach (int npcId in npcIds)
            {
                var pending = new Queue<List<int>>();
                pending.Enqueue(new List<int>());
                int runs = 0;
                while (pending.Count > 0 && runs < MaxRunsPerNpc)
                {
                    List<int> prefix = pending.Dequeue();
                    runs++;
                    conversations++;
                    bot.DrainPrompts();
                    await bot.ChatAsync($"/talk {npcId}");

                    var chosen = new List<int>();
                    while (true)
                    {
                        if (!bot.TryTakePrompt(PromptWait, out Prompt? prompt) || prompt is null)
                        {
                            break; // the script ended (or never prompted)
                        }

                        prompts++;
                        (int Action, int Selection, string Text)[] alts = Alternatives(prompt);
                        int depth = chosen.Count;
                        int pick = depth < prefix.Count && prefix[depth] < alts.Length ? prefix[depth] : 0;
                        if (depth >= prefix.Count)
                        {
                            for (int a = 1; a < alts.Length && pending.Count + runs < MaxRunsPerNpc; a++)
                            {
                                pending.Enqueue(new List<int>(chosen) { a });
                            }
                        }

                        chosen.Add(pick);
                        if (chosen.Count > MaxPromptsPerRun)
                        {
                            problems.Add($"npc {npcId}: more than {MaxPromptsPerRun} prompts on one path");
                            await bot.AnswerAsync(prompt, -1, -1, string.Empty); // escape
                            break;
                        }

                        (int action, int selection, string text) = alts[pick];
                        await bot.AnswerAsync(prompt, action, selection, text);
                    }
                }
            }

            // Processed after everything above: the answer proves the session is still alive.
            await bot.ChatAsync("/pos");
            string pong = await bot.Pong.Task.WaitAsync(cts.Token);
            Assert.StartsWith("pos:", pong);

            string[] scriptErrors = console.ToString()
                .Split('\n')
                .Where(l => l.Contains("[script]", StringComparison.Ordinal))
                .Select(l => l.Trim())
                .Distinct()
                .ToArray();
            problems.AddRange(scriptErrors);

            Assert.True(prompts > npcIds.Count, $"only {prompts} prompts over {conversations} conversations — the scripts did not run");
            Assert.True(problems.Count == 0, $"{problems.Count} problem(s) after {conversations} conversations with {npcIds.Count} NPCs:\n" + string.Join("\n", problems));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }
}
