using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The 噂の真相 rumor quests (2148–2152: one line, complete on the spot) and the 砂絵団 desert
/// supply quests (2124 / 2126: hand in the box; 2127: a briefing, then complete), driving the
/// shipped scripts/quest files. Item ids follow the JMS v186 Check data (2126 wants 4031624).
/// </summary>
public class RumorAndDesertQuestTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class Dialog : INpcDialog
    {
        private readonly BlockingCollection<int> _prompts = new();
        public int Take(CancellationToken ct) => _prompts.Take(ct);
        public void Say(int npcId, string text, bool prev, bool next) => _prompts.Add(0);
        public void AskYesNo(int npcId, string text) => _prompts.Add(2);
        public void AskMenu(int npcId, string text) => _prompts.Add(5);
        public void AskText(int npcId, string text) => _prompts.Add(3);
        public void AskAccept(int npcId, string text) => _prompts.Add(13);
        public void AskAvatar(int npcId, string text, IReadOnlyList<int> styles) => _prompts.Add(8);
        public void OpenRps(int npcId) { }
    }

    private static NpcScriptEngine Engine()
    {
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scripts", "quest")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        return new NpcScriptEngine(
            new FolderNpcScriptSource(Path.Combine(root, "scripts", "npc")),
            new FolderNpcScriptSource(Path.Combine(root, "scripts", "quest")));
    }

    private static void WaitEnded(NpcConversation cm, CancellationToken ct)
    {
        while (!cm.IsEnded && !ct.IsCancellationRequested)
        {
            Thread.Sleep(10);
        }
    }

    [Theory]
    [InlineData(2148, 1020000)]
    [InlineData(2149, 1022002)]
    [InlineData(2150, 1022007)]
    [InlineData(2151, 1022000)]
    [InlineData(2152, 1032104)]
    public void Rumor_OneLine_ThenCompletesOnTheSpot(int quest, int npc)
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(quest, npc, dialog, player, ending: false)!;
        Assert.Equal(0, dialog.Take(cts.Token));
        qm.Advance(0, 1, -1, string.Empty);
        WaitEnded(qm, cts.Token);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
        Assert.Empty(player.StartedQuests);
    }

    [Theory]
    [InlineData(2124, 4031619)]
    [InlineData(2126, 4031624)]
    public void DesertSupplies_WithTheBox_TakesIt_AndCompletes(int quest, int box)
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[box] = 1;
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(quest, 2101002, dialog, player, ending: true)!;
        Assert.Equal(0, dialog.Take(cts.Token));
        qm.Advance(0, 1, -1, string.Empty);
        WaitEnded(qm, cts.Token);
        Assert.Equal(0, player.Inventory.GetValueOrDefault(box));
        Assert.Equal(new[] { quest }, player.CompletedQuests);
    }

    [Theory]
    [InlineData(2124, 4031619)]
    [InlineData(2126, 4031624)]
    public void DesertSupplies_WithoutTheBox_IsRefused(int quest, int box)
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(quest, 2101002, dialog, player, ending: true)!;
        Assert.Equal(0, dialog.Take(cts.Token));
        qm.Advance(0, 1, -1, string.Empty);
        WaitEnded(qm, cts.Token);
        Assert.Empty(player.CompletedQuests);
        Assert.Equal(0, player.Inventory.GetValueOrDefault(box));
    }

    [Fact]
    public void AQuestScriptedOnOneSideOnly_HasNoScriptForTheOtherSide()
    {
        // 2148 scripts only its opening; 2124 only its completion — the other side must fall back
        // to the data-driven path, i.e. StartQuest returns null instead of invoking a missing function.
        Assert.Null(Engine().StartQuest(2148, 1020000, new Dialog(), new RecordingNpcPlayer(), ending: true));
        Assert.Null(Engine().StartQuest(2124, 2101002, new Dialog(), new RecordingNpcPlayer(), ending: false));
        Assert.NotNull(Engine().StartQuest(2148, 1020000, new Dialog(), new RecordingNpcPlayer(), ending: false));
    }

    [Fact]
    public void ToTheDesert_BriefingThenComplete()
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(2127, 1022002, dialog, player, ending: true)!;
        Assert.Equal(0, dialog.Take(cts.Token));
        qm.Advance(0, 1, -1, string.Empty);
        WaitEnded(qm, cts.Token);
        Assert.Equal(new[] { 2127 }, player.CompletedQuests);
    }
}
