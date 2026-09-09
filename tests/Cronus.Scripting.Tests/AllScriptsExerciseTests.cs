using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Cronus.Data;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// Runs EVERY shipped script (scripts/npc, quest, portal, reactor) headless, exploring the
/// branches of each conversation (every menu option, yes and no, accept and decline) with three
/// player profiles, and fails on anything a real client would choke on: a script that throws, a
/// prompt loop, or an item / map / mob / npc / skill id that the client's own data does not know
/// (checked against gamedata.db when it is present next to the repo, otherwise skipped).
/// This is the automatic version of "talk to every NPC" — no client needed.
/// </summary>
public class AllScriptsExerciseTests
{
    private const int MaxRunsPerProfile = 24;
    private const int MaxPromptsPerRun = 40;

    private static readonly Regex MenuOption = new(@"#L(\d+)#", RegexOptions.Compiled);
    private static readonly Regex Tag = new(@"#([tizvcmopq])(\d+)#", RegexOptions.Compiled);

    private sealed record Prompt(int Type, string Text, IReadOnlyList<int> Options);

    /// <summary>What each prompt type can be answered with: (action, selection, text).</summary>
    private static (int Action, int Selection, string Text)[] Alternatives(Prompt p) => p.Type switch
    {
        2 or 13 => new[] { (1, -1, ""), (0, -1, "") },                       // yes / no, accept / decline
        3 => new[] { (1, -1, "abc") },
        5 => p.Options.Count > 0 ? p.Options.Select(o => (1, o, "")).ToArray() : new[] { (1, 0, "") },
        8 => new[] { (1, 0, ""), (0, -1, "") },
        _ => new[] { (1, -1, "") },                                             // say: next / ok
    };

    private sealed class ProbeDialog : INpcDialog
    {
        private readonly BlockingCollection<Prompt> _prompts = new();

        public bool TryTake(TimeSpan wait, out Prompt? prompt) => _prompts.TryTake(out prompt, wait);

        public void Say(int npcId, string text, bool prev, bool next) => _prompts.Add(new Prompt(0, text, Array.Empty<int>()));
        public void AskYesNo(int npcId, string text) => _prompts.Add(new Prompt(2, text, Array.Empty<int>()));
        public void AskMenu(int npcId, string text) => _prompts.Add(new Prompt(5, text, MenuOption.Matches(text).Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList()));
        public void AskText(int npcId, string text) => _prompts.Add(new Prompt(3, text, Array.Empty<int>()));
        public void AskAccept(int npcId, string text) => _prompts.Add(new Prompt(13, text, Array.Empty<int>()));
        public void AskAvatar(int npcId, string text, IReadOnlyList<int> styles) => _prompts.Add(new Prompt(8, text, Enumerable.Range(0, Math.Max(1, styles.Count)).ToList()));
        public void OpenRps(int npcId) { }
    }

    /// <summary>A player whose state the scripts can read and change; everything they hand it is recorded for the id checks.</summary>
    private sealed class ProbePlayer : INpcPlayer
    {
        public ProbePlayer(string name, int level, int job, bool hasEverything)
        {
            Name = name;
            Level = level;
            Job = job;
            HasEverything = hasEverything;
        }

        public string Name { get; }
        public int Level { get; }
        public int Job { get; private set; }
        public bool HasEverything { get; }
        public int MapId { get; set; } = 100000000;
        public List<int> Warps { get; } = new();
        public List<int> GainedItems { get; } = new();
        public List<int> SpawnedMobs { get; } = new();
        public List<int> Shops { get; } = new();
        private readonly Dictionary<int, int> _items = new();
        private int _meso = 1_000_000, _exp, _hp = 500, _fame, _ap, _sp, _hair = 30030, _face = 20000, _skin, _buddies = 20, _remembered;
        private readonly HashSet<int> _started = new(), _done = new();
        private readonly Dictionary<int, string> _questData = new();

        public string getName() => Name;
        public int getLevel() => Level;
        public int getMapId() => MapId;
        public int getMeso() => _meso;
        public int getHp() => _hp;
        public int getMaxHp() => 500;
        public int getExp() => _exp;
        public int getGender() => 0;
        public int getJob() => Job;
        public int getStr() => 50;
        public int getDex() => 50;
        public int getInt() => 50;
        public int getLuk() => 50;
        public int getFame() => _fame;
        public int getAp() => _ap;
        public int getSp() => _sp;
        public int getHair() => _hair;
        public int getFace() => _face;
        public int getSkin() => _skin;
        public bool isValidStyle(int styleId) => true;
        public void setHair(int hairId) => _hair = hairId;
        public void setFace(int faceId) => _face = faceId;
        public void setSkin(int skinColor) => _skin = skinColor;
        public void gainMeso(int amount) => _meso = Math.Max(0, _meso + amount);
        public void gainExp(int amount) => _exp = Math.Max(0, _exp + amount);
        public void heal() => _hp = 500;
        public void rememberMap() => _remembered = MapId;
        public void warpToRememberedMap(int fallbackMapId) => warp(_remembered > 0 ? _remembered : fallbackMapId, 0);
        public void warp(int mapId, int portal = 0) { Warps.Add(mapId); MapId = mapId; }
        public void warpPortal(int mapId, string portalName) => warp(mapId, 0);
        public bool airshipBoarding() => true;
        public int airshipMinutes() => 5;
        public void openParcel() { }
        public int parcelCount() => HasEverything ? 1 : 0;
        public int receiveParcels() => 0;
        public void gainAp(int amount) => _ap = Math.Max(0, _ap + amount);
        public void gainSp(int amount) => _sp = Math.Max(0, _sp + amount);
        public void gainFame(int amount) => _fame += amount;
        public void setJob(int job) => Job = job;
        public void gainMaxHp(int amount) { }
        public void gainMaxMp(int amount) { }
        public bool hasQuest(int questId) => _started.Contains(questId);
        public bool isQuestDone(int questId) => _done.Contains(questId) || HasEverything;
        public void startQuest(int questId) => _started.Add(questId);
        public void completeQuest(int questId) { _started.Remove(questId); _done.Add(questId); }
        public void gainItem(int itemId, int quantity) { if (quantity > 0) GainedItems.Add(itemId); _items[itemId] = Math.Max(0, _items.GetValueOrDefault(itemId) + quantity); }
        public bool haveItem(int itemId) => HasEverything || itemQuantity(itemId) > 0;
        public int itemQuantity(int itemId) => HasEverything ? Math.Max(1, _items.GetValueOrDefault(itemId)) : _items.GetValueOrDefault(itemId);
        public void openShop(int shopId) => Shops.Add(shopId);
        public void openStorage() { }
        public void spawnMob(int mobId, int count) => SpawnedMobs.Add(mobId);
        public int mobCount() => 0;
        public bool hasMerchant() => false;
        public bool retrieveMerchant() => false;
        public int getBuddyCapacity() => _buddies;
        public void gainBuddyCapacity(int amount) => _buddies += amount;
        public bool isPartyLeader() => true;
        public bool startSubwayMassacre() => true;
        public bool bonusSubwayMassacre() => true;

        public int dojoPoints() => 0;
        public void setDojoPoints(int points) { }
        public bool dojoEnter(bool party, int fromStage) => false;
        public void dojoNextStage() { }
        public bool dojoTeleportUp() => false;
        public void dojoExit() { }
        public bool dojoTutorialExit() => false;
        public void openNpc(int npcId) { }
        public string? getQuestData(int questId) => _questData.TryGetValue(questId, out string? d) ? d : null;
        public void setQuestData(int questId, string data) => _questData[questId] = data;
    }

    private static ProbePlayer[] Profiles() => new[]
    {
        new ProbePlayer("Novice", 10, 0, hasEverything: false),
        new ProbePlayer("Fighter", 45, 111, hasEverything: false),
        new ProbePlayer("Hero", 200, 132, hasEverything: true),
    };

    /// <summary>The repository root (the folder holding scripts/npc), or null when the tests run elsewhere.</summary>
    private static string? RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "scripts", "npc")))
            {
                return dir.FullName;
            }
        }

        return null;
    }

    /// <summary>The client's id tables from gamedata.db, or null when the data is not on this machine.</summary>
    private sealed class KnownIds
    {
        public HashSet<int> Items { get; } = new();
        public HashSet<int> Maps { get; } = new();
        public HashSet<int> Mobs { get; } = new();
        public HashSet<int> Npcs { get; } = new();
        public HashSet<int> Skills { get; } = new();
        public IWzStore Store { get; }

        private KnownIds(IWzStore store) => Store = store;

        public static KnownIds? Load(string root)
        {
            string db = Path.Combine(root, "gamedata.db");
            if (!File.Exists(db))
            {
                return null;
            }

            var store = new SqliteWzStore(db);
            var known = new KnownIds(store);
            foreach (string table in new[] { "Cash", "Consume", "Eqp", "Etc", "Ins", "Pet" })
            {
                Collect(store, $"String/{table}.img.xml", known.Items, minDepth: 1);
            }

            Collect(store, "String/Map.img.xml", known.Maps, minDepth: 2);
            Collect(store, "String/Mob.img.xml", known.Mobs, minDepth: 1);
            Collect(store, "String/Npc.img.xml", known.Npcs, minDepth: 1);
            Collect(store, "String/Skill.img.xml", known.Skills, minDepth: 1);
            return known;
        }

        public bool MapExists(int mapId) => Maps.Contains(mapId) || Store.Exists($"Map/Map{mapId / 100000000}/{mapId}.img.xml");

        private static void Collect(IWzStore store, string path, HashSet<int> into, int minDepth)
        {
            string? xml = store.ReadText(path);
            if (xml is null)
            {
                return;
            }

            Walk(WzData.ParseText(xml), 1, minDepth, into);
        }

        private static void Walk(WzData node, int depth, int minDepth, HashSet<int> into)
        {
            foreach ((string name, WzData child) in node.Children)
            {
                if (depth >= minDepth && int.TryParse(name, out int id))
                {
                    into.Add(id);
                }

                Walk(child, depth + 1, minDepth, into);
            }
        }
    }

    private static IEnumerable<int> ScriptIds(string folder)
        => Directory.EnumerateFiles(folder, "*.js")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Where(n => int.TryParse(n, out _))
            .Select(int.Parse)
            .OrderBy(id => id);

    /// <summary>Runs one NPC's script against one profile along every branch (bounded), collecting problems and texts.</summary>
    private static void Explore(NpcScriptEngine engine, int npcId, Func<ProbePlayer> profileFactory, List<string> problems, List<string> texts, List<ProbePlayer> usedPlayers)
    {
        var pending = new Queue<List<int>>();
        pending.Enqueue(new List<int>());
        int runs = 0;
        while (pending.Count > 0 && runs < MaxRunsPerProfile)
        {
            List<int> prefix = pending.Dequeue();
            runs++;
            ProbePlayer player = profileFactory();
            usedPlayers.Add(player);
            var dialog = new ProbeDialog();
            NpcConversation? cm = engine.Start(npcId, dialog, player);
            if (cm is null)
            {
                problems.Add($"npc {npcId}: the engine found no script");
                return;
            }

            var chosen = new List<int>();
            var clock = Stopwatch.StartNew();
            while (true)
            {
                Prompt? prompt = null;
                while (!dialog.TryTake(TimeSpan.FromMilliseconds(30), out prompt))
                {
                    if (cm.IsEnded)
                    {
                        break;
                    }

                    if (clock.Elapsed > TimeSpan.FromSeconds(5))
                    {
                        problems.Add($"npc {npcId} [{player.Name}] path {string.Join(",", chosen)}: script neither prompted nor ended for 5 s");
                        cm.End();
                        break;
                    }
                }

                if (prompt is null)
                {
                    break;
                }

                clock.Restart();
                texts.Add(prompt.Text);
                (int Action, int Selection, string Text)[] alts = Alternatives(prompt);
                int depth = chosen.Count;
                int pick = depth < prefix.Count && prefix[depth] < alts.Length ? prefix[depth] : 0;
                if (depth >= prefix.Count)
                {
                    for (int a = 1; a < alts.Length && pending.Count + runs < MaxRunsPerProfile; a++)
                    {
                        var branch = new List<int>(chosen) { a };
                        pending.Enqueue(branch);
                    }
                }

                chosen.Add(pick);
                if (chosen.Count > MaxPromptsPerRun)
                {
                    problems.Add($"npc {npcId} [{player.Name}]: more than {MaxPromptsPerRun} prompts on one path (a loop?)");
                    cm.End();
                    break;
                }

                (int action, int selection, string text) = alts[pick];
                cm.Advance(prompt.Type, action, selection, text);
            }

            var end = Stopwatch.StartNew();
            while (!cm.IsEnded && end.Elapsed < TimeSpan.FromSeconds(3))
            {
                Thread.Sleep(10);
            }

            if (cm.Error is not null)
            {
                // One line per distinct failure: the same broken call shows up on every path and profile.
                string line = $"npc {npcId}: {cm.Error.GetType().Name}: {cm.Error.Message}";
                if (!problems.Any(p => p.StartsWith(line, StringComparison.Ordinal)))
                {
                    problems.Add(line + $"  (first seen: [{player.Name}] path {string.Join(",", chosen)})");
                }
            }
        }
    }

    private static void CheckIds(KnownIds known, string where, IEnumerable<string> texts, IEnumerable<ProbePlayer> players, List<string> problems)
    {
        var seen = new HashSet<string>();
        void Report(string kind, int id, string origin)
        {
            if (seen.Add($"{kind}:{id}:{where}"))
            {
                problems.Add($"{where}: unknown {kind} {id} ({origin})");
            }
        }

        foreach (string text in texts)
        {
            foreach (Match m in Tag.Matches(text))
            {
                int id = int.Parse(m.Groups[2].Value);
                switch (m.Groups[1].Value)
                {
                    case "t" or "i" or "z" or "v" or "c": if (!known.Items.Contains(id)) Report("item", id, "dialog tag"); break;
                    case "m": if (!known.MapExists(id)) Report("map", id, "dialog tag"); break;
                    case "o": if (!known.Mobs.Contains(id)) Report("mob", id, "dialog tag"); break;
                    case "p": if (!known.Npcs.Contains(id)) Report("npc", id, "dialog tag"); break;
                    case "q": if (!known.Skills.Contains(id)) Report("skill", id, "dialog tag"); break;
                }
            }
        }

        foreach (ProbePlayer p in players)
        {
            foreach (int map in p.Warps.Where(map => map != 999999999 && !known.MapExists(map))) Report("map", map, "warp");
            foreach (int item in p.GainedItems.Where(item => !known.Items.Contains(item))) Report("item", item, "gainItem");
            foreach (int mob in p.SpawnedMobs.Where(mob => !known.Mobs.Contains(mob))) Report("mob", mob, "spawnMob");
        }
    }

    [Fact]
    public void EveryNpcScript_RunsCleanOnEveryBranch_AndNamesOnlyKnownIds()
    {
        string? root = RepoRoot();
        if (root is null)
        {
            return; // not a repo checkout (CI package layout): nothing to exercise
        }

        string npcDir = Path.Combine(root, "scripts", "npc");
        string questDir = Path.Combine(root, "scripts", "quest");
        var engine = new NpcScriptEngine(new FolderNpcScriptSource(npcDir), new FolderNpcScriptSource(questDir), answerTimeoutMs: 5_000);
        KnownIds? known = KnownIds.Load(root);

        var problems = new List<string>();
        int scripts = 0;
        foreach (int npcId in ScriptIds(npcDir))
        {
            scripts++;
            var texts = new List<string>();
            var players = new List<ProbePlayer>();
            foreach (ProbePlayer profile in Profiles())
            {
                Explore(engine, npcId, () => new ProbePlayer(profile.Name, profile.Level, profile.Job, profile.HasEverything), problems, texts, players);
            }

            if (known is not null)
            {
                CheckIds(known, $"npc {npcId}", texts, players, problems);
                if (!known.Store.Exists($"Npc/{npcId:D7}.img.xml"))
                {
                    // The client crashes (0x80030002) when a dialog opens for an NPC it has no image for.
                    problems.Add($"npc {npcId}: no client image in Npc.wz — a dialog for it crashes the client; the script targets an id this client does not have");
                }
            }
        }

        Assert.True(scripts > 0, "no NPC scripts found");
        Assert.True(problems.Count == 0, $"{problems.Count} problem(s) in {scripts} NPC scripts:\n" + string.Join("\n", problems));
    }

    [Fact]
    public void EveryQuestScript_RunsCleanForStartAndEnd()
    {
        string? root = RepoRoot();
        if (root is null)
        {
            return;
        }

        string questDir = Path.Combine(root, "scripts", "quest");
        if (!Directory.Exists(questDir))
        {
            return;
        }

        var engine = new NpcScriptEngine(new DictionaryNpcScriptSource(new Dictionary<int, string>()), new FolderNpcScriptSource(questDir), answerTimeoutMs: 5_000);
        var problems = new List<string>();
        foreach (int questId in ScriptIds(questDir))
        {
            foreach (bool ending in new[] { false, true })
            {
                var dialog = new ProbeDialog();
                var player = new ProbePlayer("Hero", 200, 132, hasEverything: true);
                NpcConversation? qm = engine.StartQuest(questId, 0, dialog, player, ending);
                if (qm is null)
                {
                    continue;
                }

                var clock = Stopwatch.StartNew();
                int prompts = 0;
                while (!qm.IsEnded && clock.Elapsed < TimeSpan.FromSeconds(5) && prompts < MaxPromptsPerRun)
                {
                    if (dialog.TryTake(TimeSpan.FromMilliseconds(30), out Prompt? p) && p is not null)
                    {
                        prompts++;
                        (int action, int selection, string text) = Alternatives(p)[0];
                        qm.Advance(p.Type, action, selection, text);
                        clock.Restart();
                    }
                }

                if (!qm.IsEnded)
                {
                    qm.End();
                }

                if (qm.Error is not null)
                {
                    problems.Add($"quest {questId} {(ending ? "end" : "start")}(): {qm.Error.Message}");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void EveryPortalAndReactorScript_RunsClean_AndWarpsToKnownMaps()
    {
        string? root = RepoRoot();
        if (root is null)
        {
            return;
        }

        KnownIds? known = KnownIds.Load(root);
        var problems = new List<string>();
        foreach (string kind in new[] { "portal", "reactor" })
        {
            string dir = Path.Combine(root, "scripts", kind);
            if (!Directory.Exists(dir))
            {
                continue;
            }

            var engine = new PortalScriptEngine(new FolderPortalScriptSource(dir));
            string current = string.Empty;
            engine.ScriptFailed += (name, ex) => problems.Add($"{kind} {name}: {ex.Message}");
            foreach (string file in Directory.EnumerateFiles(dir, "*.js"))
            {
                current = Path.GetFileNameWithoutExtension(file);
                foreach (ProbePlayer profile in Profiles())
                {
                    var player = new ProbePlayer(profile.Name, profile.Level, profile.Job, profile.HasEverything);
                    engine.Run(current, player);
                    if (known is not null)
                    {
                        CheckIds(known, $"{kind} {current}", Array.Empty<string>(), new[] { player }, problems);
                    }
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }
}
