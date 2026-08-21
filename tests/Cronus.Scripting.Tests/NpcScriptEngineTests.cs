using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// Exercises the blocking-thread NPC conversation model end to end with an in-memory dialog:
/// the script drives prompts, the test plays the "client" by feeding answers, and the recorded
/// message sequence is asserted.
/// </summary>
public class NpcScriptEngineTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed record Prompt(int MessageType, string Text, bool Prev, bool Next);

    /// <summary>Records prompts and signals each one so the test can answer in lockstep.</summary>
    private sealed class RecordingDialog : INpcDialog
    {
        private readonly BlockingCollection<Prompt> _prompts = new();

        public IReadOnlyList<Prompt> Recorded => _recorded;
        private readonly List<Prompt> _recorded = new();

        public Prompt Take(CancellationToken ct) => _prompts.Take(ct);

        public void Say(int npcId, string text, bool prev, bool next) => Emit(new Prompt(0, text, prev, next));
        public void AskYesNo(int npcId, string text) => Emit(new Prompt(2, text, false, false));
        public void AskMenu(int npcId, string text) => Emit(new Prompt(5, text, false, false));
        public void AskText(int npcId, string text) => Emit(new Prompt(3, text, false, false));
        public void AskAccept(int npcId, string text) => Emit(new Prompt(13, text, false, false));

        private void Emit(Prompt p)
        {
            _recorded.Add(p);
            _prompts.Add(p);
        }
    }

    [Fact]
    public void MultiStepDialog_MenuThenTextThenOk()
    {
        const int npcId = 9000000;
        const string script = """
            function start() {
                var pick = cm.askMenu("Choose:\r\n#L0#Sword#l\r\n#L1#Shield#l");
                if (pick == 0) {
                    var name = cm.askText("Name your sword:");
                    cm.sendOk("A fine blade, " + name + "!");
                } else {
                    cm.sendOk("A sturdy shield.");
                }
            }
            """;

        var source = new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcId] = script });
        var engine = new NpcScriptEngine(source);
        var dialog = new RecordingDialog();

        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation? cm = engine.Start(npcId, dialog, player: null);
        Assert.NotNull(cm);

        // 1) menu prompt -> pick option 0.
        Prompt menu = dialog.Take(cts.Token);
        Assert.Equal(5, menu.MessageType);
        Assert.Contains("Choose", menu.Text);
        Assert.True(cm!.Advance(messageType: 5, action: 1, selection: 0, text: string.Empty));

        // 2) text prompt -> answer "Excalibur".
        Prompt text = dialog.Take(cts.Token);
        Assert.Equal(3, text.MessageType);
        Assert.True(cm.Advance(messageType: 3, action: 1, selection: -1, text: "Excalibur"));

        // 3) final say (ok) -> confirm content, then answer to let the script finish.
        Prompt ok = dialog.Take(cts.Token);
        Assert.Equal(0, ok.MessageType);
        Assert.Equal("A fine blade, Excalibur!", ok.Text);
        Assert.False(ok.Next); // sendOk => no next button
        cm.Advance(messageType: 0, action: 1, selection: -1, text: string.Empty);

        WaitUntilEnded(cm, cts.Token);
        Assert.True(cm.IsEnded);
    }

    [Fact]
    public void AskYesNo_ReturnsChoice()
    {
        const int npcId = 9000001;
        const string script = """
            function start() {
                if (cm.askYesNo("Ready?")) {
                    cm.sendOk("Great!");
                } else {
                    cm.sendOk("Maybe later.");
                }
            }
            """;

        var engine = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcId] = script }));
        var dialog = new RecordingDialog();

        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = engine.Start(npcId, dialog, null)!;

        Prompt yesno = dialog.Take(cts.Token);
        Assert.Equal(2, yesno.MessageType);
        cm.Advance(messageType: 2, action: 0, selection: -1, text: string.Empty); // "No"

        Prompt ok = dialog.Take(cts.Token);
        Assert.Equal("Maybe later.", ok.Text);
    }

    [Fact]
    public void Escape_EndsConversation()
    {
        const int npcId = 9000002;
        const string script = """
            function start() {
                cm.sendNext("Line 1");
                cm.sendNext("Line 2 (never reached)");
            }
            """;

        var engine = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcId] = script }));
        var dialog = new RecordingDialog();

        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = engine.Start(npcId, dialog, null)!;

        Prompt first = dialog.Take(cts.Token);
        Assert.Equal("Line 1", first.Text);

        cm.Advance(messageType: 0, action: -1, selection: -1, text: string.Empty); // escape

        WaitUntilEnded(cm, cts.Token);
        Assert.True(cm.IsEnded);
        Assert.Single(dialog.Recorded); // second line never sent
    }

    [Fact]
    public void UnknownNpc_ReturnsNull()
    {
        var engine = new NpcScriptEngine(new DictionaryNpcScriptSource(new Dictionary<int, string>()));
        Assert.Null(engine.Start(123, new RecordingDialog(), null));
    }

    private sealed class FakePlayer : INpcPlayer
    {
        public int Meso;
        public int Exp;
        public int Hp = 10;
        public int SaveCount;

        public string getName() => "Hero";
        public int getLevel() => 10;
        public int getMapId() => 100000000;
        public int getMeso() => Meso;
        public int getHp() => Hp;
        public int getMaxHp() => 100;
        public int getExp() => Exp;
        public int getGender() => 0;
        public int getJob() => Job;
        public int getStr() => 4;
        public int getDex() => 4;
        public int getInt() => 4;
        public int getLuk() => 4;
        public int getFame() => Fame;
        public int getAp() => Ap;
        public int getSp() => Sp;
        public void gainMeso(int amount)
        {
            Meso = Math.Max(0, Meso + amount);
            SaveCount++;
        }
        public void gainExp(int amount) => Exp = Math.Max(0, Exp + amount);
        public void heal() => Hp = getMaxHp();

        public int Job;
        public int Fame;
        public int Ap;
        public int Sp;
        public int WarpedTo = -1;
        public void warp(int mapId) => warp(mapId, 0);
        public void warp(int mapId, int portal) => WarpedTo = mapId;
        public void gainAp(int amount) => Ap = Math.Max(0, Ap + amount);
        public void gainSp(int amount) => Sp = Math.Max(0, Sp + amount);
        public void gainFame(int amount) => Fame = Math.Clamp(Fame + amount, -30000, 30000);
        public void setJob(int job) => Job = job;

        public HashSet<int> Started { get; } = new();
        public HashSet<int> Completed { get; } = new();
        public bool hasQuest(int questId) => Started.Contains(questId);
        public bool isQuestDone(int questId) => Completed.Contains(questId);
        public void startQuest(int questId) => Started.Add(questId);
        public void completeQuest(int questId) { Started.Remove(questId); Completed.Add(questId); }
    }

    [Fact]
    public void ScriptCanReadAndMutatePlayer()
    {
        const int npcId = 9000003;
        const string script = """
            function start() {
                if (player.getLevel() >= 10) {
                    player.gainMeso(1000);
                    cm.sendOk(player.getName() + " now has " + player.getMeso() + " mesos.");
                }
            }
            """;

        var engine = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcId] = script }));
        var dialog = new RecordingDialog();
        var player = new FakePlayer { Meso = 500 };

        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = engine.Start(npcId, dialog, player)!;

        Prompt ok = dialog.Take(cts.Token);
        Assert.Equal("Hero now has 1500 mesos.", ok.Text);
        Assert.Equal(1500, player.Meso);
        Assert.Equal(1, player.SaveCount);

        cm.Advance(messageType: 0, action: 1, selection: -1, text: string.Empty);
    }

    [Fact]
    public void ScriptCanDriveQuestFlow()
    {
        const int npcId = 9000005;
        const int questId = 1000;
        const string script = """
            function start() {
                if (player.isQuestDone(1000)) {
                    cm.sendOk("You already finished this.");
                } else if (player.hasQuest(1000)) {
                    player.completeQuest(1000);
                    cm.sendOk("Quest complete!");
                } else {
                    player.startQuest(1000);
                    cm.sendOk("Quest started.");
                }
            }
            """;

        var source = new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcId] = script });
        var engine = new NpcScriptEngine(source);
        var player = new FakePlayer();

        // First talk: starts the quest.
        RunOnce(engine, npcId, player, out string first);
        Assert.Equal("Quest started.", first);
        Assert.Contains(questId, player.Started);

        // Second talk: completes it.
        RunOnce(engine, npcId, player, out string second);
        Assert.Equal("Quest complete!", second);
        Assert.Contains(questId, player.Completed);
        Assert.DoesNotContain(questId, player.Started);

        // Third talk: already done.
        RunOnce(engine, npcId, player, out string third);
        Assert.Equal("You already finished this.", third);
    }

    private static void RunOnce(NpcScriptEngine engine, int npcId, FakePlayer player, out string okText)
    {
        var dialog = new RecordingDialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = engine.Start(npcId, dialog, player)!;
        Prompt ok = dialog.Take(cts.Token);
        okText = ok.Text;
        cm.Advance(messageType: 0, action: 1, selection: -1, text: string.Empty);
        while (!cm.IsEnded)
        {
            cts.Token.ThrowIfCancellationRequested();
            Thread.Sleep(5);
        }
    }

    [Fact]
    public void ScriptCanHealAndGiveExp()
    {
        const int npcId = 9000004;
        const string script = """
            function start() {
                player.heal();
                player.gainExp(500);
                cm.sendOk("Healed to " + player.getHp() + ", exp " + player.getExp());
            }
            """;

        var engine = new NpcScriptEngine(
            new DictionaryNpcScriptSource(new Dictionary<int, string> { [npcId] = script }));
        var dialog = new RecordingDialog();
        var player = new FakePlayer { Hp = 10, Exp = 100 };

        using var cts = new CancellationTokenSource(Timeout);
        engine.Start(npcId, dialog, player);

        Prompt ok = dialog.Take(cts.Token);
        Assert.Equal("Healed to 100, exp 600", ok.Text);
        Assert.Equal(100, player.Hp);
        Assert.Equal(600, player.Exp);
    }

    private static void WaitUntilEnded(NpcConversation cm, CancellationToken ct)
    {
        while (!cm.IsEnded)
        {
            ct.ThrowIfCancellationRequested();
            Thread.Sleep(5);
        }
    }
}
