using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// ローザーとリンゴ (quest 1021): the opening drops HP, hands out Roger's Apple and starts the
/// quest; the ending refuses under 50 HP, otherwise pays 3 リンゴ + 3 緑リンゴ + 10 exp and
/// completes. Drives the shipped scripts/quest/1021.js through the quest entry points.
/// </summary>
public class RogerTutorialQuestTests
{
    private const int Quest = 1021;
    private const int Roger = 2000;
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

    [Fact]
    public void Start_Accept_DropsHp_GivesTheApple_StartsTheQuest()
    {
        var player = new RecordingNpcPlayer { Hp = 100, Level = 1 };
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(Quest, Roger, dialog, player, ending: false)!;

        Assert.Equal(0, dialog.Take(cts.Token)); qm.Advance(0, 1, -1, string.Empty);
        Assert.Equal(0, dialog.Take(cts.Token)); qm.Advance(0, 1, -1, string.Empty);
        Assert.Equal(13, dialog.Take(cts.Token)); qm.Advance(13, 1, -1, string.Empty);   // accept
        Assert.Equal(0, dialog.Take(cts.Token)); qm.Advance(0, 1, -1, string.Empty);
        Assert.Equal(0, dialog.Take(cts.Token)); qm.Advance(0, 1, -1, string.Empty);
        WaitEnded(qm, cts.Token);

        Assert.Equal(25, player.Hp);
        Assert.Equal(1, player.Inventory.GetValueOrDefault(2010007));   // ローザーのリンゴ
        Assert.Equal(new[] { Quest }, player.StartedQuests);
    }

    [Fact]
    public void Start_Decline_ChangesNothing()
    {
        var player = new RecordingNpcPlayer { Hp = 100 };
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(Quest, Roger, dialog, player, ending: false)!;
        Assert.Equal(0, dialog.Take(cts.Token)); qm.Advance(0, 1, -1, string.Empty);
        Assert.Equal(0, dialog.Take(cts.Token)); qm.Advance(0, 1, -1, string.Empty);
        Assert.Equal(13, dialog.Take(cts.Token)); qm.Advance(13, 0, -1, string.Empty);   // decline
        Assert.Equal(0, dialog.Take(cts.Token)); qm.Advance(0, 1, -1, string.Empty);
        WaitEnded(qm, cts.Token);
        Assert.Equal(100, player.Hp);
        Assert.Empty(player.StartedQuests);
        Assert.Equal(0, player.Inventory.GetValueOrDefault(2010007));
    }

    [Fact]
    public void End_Recovered_PaysTheGift_AndCompletes()
    {
        var player = new RecordingNpcPlayer { Hp = 100 };
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(Quest, Roger, dialog, player, ending: true)!;
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(0, dialog.Take(cts.Token));
            qm.Advance(0, 1, -1, string.Empty);
        }

        WaitEnded(qm, cts.Token);
        Assert.Equal(10, player.Exp);
        Assert.Equal(3, player.Inventory.GetValueOrDefault(2010000));
        Assert.Equal(3, player.Inventory.GetValueOrDefault(2010009));
        Assert.Equal(new[] { Quest }, player.CompletedQuests);
    }

    [Fact]
    public void End_StillHurt_IsRefused()
    {
        var player = new RecordingNpcPlayer { Hp = 30 };
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(Quest, Roger, dialog, player, ending: true)!;
        Assert.Equal(0, dialog.Take(cts.Token)); qm.Advance(0, 1, -1, string.Empty);
        WaitEnded(qm, cts.Token);
        Assert.Empty(player.CompletedQuests);
        Assert.Equal(0, player.Exp);
    }
}
