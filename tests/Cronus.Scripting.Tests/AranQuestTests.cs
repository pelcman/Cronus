using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The Aran quests: basic training 21015–21018 (start only), 5人の英雄 21100 and the pole-arm
/// awakening 21101 (Legend 2000 → Aran 2100 with the stat reset), the second-job trio 21200 /
/// 21202 / 21201 (2100 → 2110, hidden record 21203 = "0" for 仙人翁), the third-job set 21300 /
/// 21301 / 21302 / 21303 (2110 → 2111, hidden record 21203 = "1" for ティティティ), and プニの頼み
/// 21600. Medals stay with the title quests (29924–29926) as in JMS.
/// </summary>
public class AranQuestTests
{
    private const int Lilin = 1201000;
    private const int Maha = 1201002;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class Dialog : INpcDialog
    {
        private readonly BlockingCollection<int> _prompts = new();
        public bool TryTake(out int type, int ms) => _prompts.TryTake(out type, ms);
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

    private static void Run(int quest, int npc, RecordingNpcPlayer player, bool ending, int yes = 1, int accept = 1)
    {
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(quest, npc, dialog, player, ending)!;
        while (!qm.IsEnded && !cts.IsCancellationRequested)
        {
            if (dialog.TryTake(out int type, 20))
            {
                qm.Advance(type, type switch { 2 => yes, 13 => accept, _ => 1 }, -1, string.Empty);
            }
        }

        Assert.True(qm.IsEnded, "conversation did not end");
    }

    [Theory]
    [InlineData(21015)]
    [InlineData(21016)]
    [InlineData(21017)]
    [InlineData(21018)]
    public void BasicTraining_AcceptStarts_DeclineDoesNot(int quest)
    {
        var yes = new RecordingNpcPlayer { Job = 2000, Level = 6 };
        Run(quest, Lilin, yes, ending: false, accept: 1);
        Assert.Equal(new[] { quest }, yes.StartedQuests);

        var no = new RecordingNpcPlayer { Job = 2000, Level = 6 };
        Run(quest, Lilin, no, ending: false, accept: 0);
        Assert.Empty(no.StartedQuests);
    }

    [Fact]
    public void FiveHeroes_AcceptCompletesOnTheSpot()
    {
        var player = new RecordingNpcPlayer { Job = 2000, Level = 10 };
        Run(21100, Lilin, player, ending: false, accept: 1);
        Assert.Equal(new[] { 21100 }, player.CompletedQuests);

        var declined = new RecordingNpcPlayer { Job = 2000, Level = 10 };
        Run(21100, Lilin, declined, ending: false, accept: 0);
        Assert.Empty(declined.CompletedQuests);
    }

    [Fact]
    public void PoleArm_Yes_AwakensTheAran_ResetsStats_Completes()
    {
        var player = new RecordingNpcPlayer { Job = 2000, Level = 10 };
        Run(21101, 1201001, player, ending: false, yes: 1);
        Assert.Equal(new[] { 2100 }, player.JobChanges);
        Assert.True(player.StatsReset);
        Assert.Equal(new[] { 21101 }, player.CompletedQuests);
        Assert.Equal(0, player.Inventory.GetValueOrDefault(1142129)); // the medal is 29924's
    }

    [Fact]
    public void PoleArm_NoOrAlreadyAran_ChangesNothing()
    {
        var no = new RecordingNpcPlayer { Job = 2000, Level = 10 };
        Run(21101, 1201001, no, ending: false, yes: 0);
        Assert.Empty(no.JobChanges);
        Assert.Empty(no.CompletedQuests);

        var already = new RecordingNpcPlayer { Job = 2100, Level = 10 };
        Run(21101, 1201001, already, ending: false, yes: 1);
        Assert.Empty(already.JobChanges);
        Assert.Empty(already.CompletedQuests);
    }

    [Fact]
    public void WeaponWaitingForItsMaster_StartAtLilin_EndAtMaha_FlagsTheHiddenRecord()
    {
        var player = new RecordingNpcPlayer { Job = 2100, Level = 30 };
        Run(21200, Lilin, player, ending: false, accept: 1);
        Assert.Equal(new[] { 21200 }, player.StartedQuests);

        Run(21200, Maha, player, ending: true, yes: 1);
        Assert.Equal(new[] { 21200 }, player.CompletedQuests);
        Assert.Equal("0", player.QuestData[21203]);
    }

    [Fact]
    public void BestWeapon_StartsAtTheSmith_EndsWithThirtyTokens()
    {
        var player = new RecordingNpcPlayer { Job = 2100, Level = 30 };
        Run(21202, 1203000, player, ending: false, accept: 1);
        Assert.Equal(new[] { 21202 }, player.StartedQuests);

        player.Inventory[4032311] = 29;
        Run(21202, 1203000, player, ending: true);
        Assert.Empty(player.CompletedQuests);
        Assert.Equal(29, player.Inventory[4032311]);

        player.Inventory[4032311] = 31;
        Run(21202, 1203000, player, ending: true, yes: 1);
        Assert.Equal(new[] { 21202 }, player.CompletedQuests);
        Assert.Equal(1, player.Inventory[4032311]);
    }

    [Fact]
    public void Maha_SecondJob_AdvancesAnAran()
    {
        var player = new RecordingNpcPlayer { Job = 2100, Level = 30 };
        Run(21201, Maha, player, ending: true, accept: 1);
        Assert.Equal(new[] { 2110 }, player.JobChanges);
        Assert.Equal(new[] { 21201 }, player.CompletedQuests);

        var declined = new RecordingNpcPlayer { Job = 2100, Level = 30 };
        Run(21201, Maha, declined, ending: true, accept: 0);
        Assert.Empty(declined.JobChanges);
        Assert.Empty(declined.CompletedQuests);
    }

    [Fact]
    public void ThirdJobChapter_StartsThiefEndsRedJadeAdvances()
    {
        var player = new RecordingNpcPlayer { Job = 2110, Level = 70 };
        Run(21300, Lilin, player, ending: false, accept: 1);
        Assert.Equal(new[] { 21300 }, player.StartedQuests);

        Run(21301, Maha, player, ending: true);
        Assert.Contains(21301, player.CompletedQuests);
        Assert.Equal("1", player.QuestData[21203]);

        Run(21302, Maha, player, ending: true);
        Assert.Empty(player.JobChanges); // no jade yet

        player.Inventory[4032312] = 1;
        Run(21302, Maha, player, ending: true);
        Assert.Equal(0, player.Inventory[4032312]);
        Assert.Equal(new[] { 2111 }, player.JobChanges);
        Assert.Contains(21302, player.CompletedQuests);

        Run(21303, 1203001, player, ending: false);
        Assert.Contains(21303, player.StartedQuests);
    }

    [Fact]
    public void PunisRequest_AcceptStarts()
    {
        var player = new RecordingNpcPlayer { Job = 2111, Level = 50 };
        Run(21600, 1202007, player, ending: false, accept: 1);
        Assert.Equal(new[] { 21600 }, player.StartedQuests);
    }
}
