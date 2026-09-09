using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The Cygnus Knights advancement quests: 選択の岐路 20100, the five first-job quests 20101–20105
/// (starter weapon, changeJob, stat reset), 見習い騎士の終わり 20200, the five knight-class exams
/// 20201–20205 (30 tokens, SP must be spent), the five 神獣の涙 20311–20315 (3rd job), the Lv120
/// chain 20400 / 20401 / 20405 / 20406 / 20408 (X11 → X12), and the Nineheart odds and ends
/// 20520 / 20600 / 20610 / 20700 / 20710 / 20720. Medals are left to the title quests as in JMS.
/// </summary>
public class CygnusKnightQuestTests
{
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

    [Fact]
    public void Crossroads_AcceptStartsAndCompletes()
    {
        var player = new RecordingNpcPlayer { Job = 1000, Level = 10 };
        Run(20100, 1101002, player, ending: false);
        Assert.Equal(new[] { 20100 }, player.CompletedQuests);
    }

    public static IEnumerable<object[]> FirstJobs() => new[]
    {
        new object[] { 20101, 1101003, 1100, 1302077 },
        new object[] { 20102, 1101004, 1200, 1372043 },
        new object[] { 20103, 1101005, 1300, 1452051 },
        new object[] { 20104, 1101006, 1400, 1472061 },
        new object[] { 20105, 1101007, 1500, 1482014 },
    };

    [Theory]
    [MemberData(nameof(FirstJobs))]
    public void FirstJob_Yes_GivesTheWeapon_ChangesJob_ResetsStats_Completes(int quest, int npc, int job, int weapon)
    {
        var player = new RecordingNpcPlayer { Job = 1000, Level = 10 };
        Run(quest, npc, player, ending: true, yes: 1);
        Assert.Equal(1, player.Inventory[weapon]);
        Assert.Equal(new[] { job }, player.JobChanges);
        Assert.True(player.StatsReset);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
        Assert.Equal(0, player.Inventory.GetValueOrDefault(1142066)); // the medal is the title quest's (29906)
    }

    [Theory]
    [MemberData(nameof(FirstJobs))]
    public void FirstJob_NoOrWrongJob_ChangesNothing(int quest, int npc, int job, int weapon)
    {
        _ = job;
        var declined = new RecordingNpcPlayer { Job = 1000, Level = 10 };
        Run(quest, npc, declined, ending: true, yes: 0);
        Assert.Empty(declined.JobChanges);
        Assert.Equal(0, declined.Inventory.GetValueOrDefault(weapon));
        Assert.Empty(declined.CompletedQuests);

        var already = new RecordingNpcPlayer { Job = 1100, Level = 10 };
        Run(quest, npc, already, ending: true, yes: 1);
        Assert.Empty(already.JobChanges);
        Assert.Empty(already.CompletedQuests);
    }

    [Fact]
    public void TraineeEnd_AcceptStartsAndCompletes()
    {
        var player = new RecordingNpcPlayer { Job = 1100, Level = 30 };
        Run(20200, 1101002, player, ending: false, accept: 1);
        Assert.Equal(new[] { 20200 }, player.CompletedQuests);
    }

    public static IEnumerable<object[]> SecondJobs() => new[]
    {
        new object[] { 20201, 1101003, 1100, 1110, 4032096 },
        new object[] { 20202, 1101004, 1200, 1210, 4032097 },
        new object[] { 20203, 1101005, 1300, 1310, 4032098 },
        new object[] { 20204, 1101006, 1400, 1410, 4032099 },
        new object[] { 20205, 1101007, 1500, 1510, 4032100 },
    };

    [Theory]
    [MemberData(nameof(SecondJobs))]
    public void KnightExam_TakesTheTokens_AndAdvances(int quest, int npc, int from, int to, int token)
    {
        var player = new RecordingNpcPlayer { Job = from, Level = 30, Sp = 0 };
        player.Inventory[token] = 30;
        Run(quest, npc, player, ending: true, yes: 1);
        Assert.Equal(0, player.Inventory[token]);
        Assert.Equal(new[] { to }, player.JobChanges);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
    }

    [Theory]
    [MemberData(nameof(SecondJobs))]
    public void KnightExam_UnspentSp_IsRefused(int quest, int npc, int from, int to, int token)
    {
        _ = to;
        var player = new RecordingNpcPlayer { Job = from, Level = 30, Sp = 4 };
        player.Inventory[token] = 30;
        Run(quest, npc, player, ending: true, yes: 1);
        Assert.Equal(30, player.Inventory[token]);
        Assert.Empty(player.JobChanges);
        Assert.Empty(player.CompletedQuests);
    }

    public static IEnumerable<object[]> ThirdJobs() => new[]
    {
        new object[] { 20311, 1101003, 1110, 1111 },
        new object[] { 20312, 1101004, 1210, 1211 },
        new object[] { 20313, 1101005, 1310, 1311 },
        new object[] { 20314, 1101006, 1410, 1411 },
        new object[] { 20315, 1101007, 1510, 1511 },
    };

    [Theory]
    [MemberData(nameof(ThirdJobs))]
    public void ShinsooTears_Advances_WhenSpIsSpent(int quest, int npc, int from, int to)
    {
        var player = new RecordingNpcPlayer { Job = from, Level = 72, Sp = 6 }; // (72-70)*3 = 6 allowed
        Run(quest, npc, player, ending: false, yes: 1);
        Assert.Equal(new[] { to }, player.JobChanges);
        Assert.Equal(new[] { quest }, player.CompletedQuests);

        var unspent = new RecordingNpcPlayer { Job = from, Level = 70, Sp = 1 };
        Run(quest, npc, unspent, ending: false, yes: 1);
        Assert.Empty(unspent.JobChanges);
        Assert.Empty(unspent.CompletedQuests);
    }

    [Theory]
    [InlineData(20400, 1101002)]
    [InlineData(20401, 2020006)]
    [InlineData(20405, 2081013)]
    [InlineData(20406, 1101002)]
    [InlineData(20520, 1101002)]
    [InlineData(20700, 1101002)]
    public void OnTheSpotChapters_Complete(int quest, int npc)
    {
        var player = new RecordingNpcPlayer { Job = 1111, Level = 120 };
        Run(quest, npc, player, ending: false);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
    }

    [Theory]
    [InlineData(1111, 1112)]
    [InlineData(1511, 1512)]
    public void ChiefKnight_PromotesAnAdvancedKnight(int from, int to)
    {
        var player = new RecordingNpcPlayer { Job = from, Level = 120 };
        Run(20408, 1101000, player, ending: false, accept: 1);
        Assert.Equal(new[] { to }, player.JobChanges);
        Assert.Equal(new[] { 20408 }, player.CompletedQuests);
        Assert.Equal(0, player.Inventory.GetValueOrDefault(1142069)); // the medal is 29909's
    }

    [Fact]
    public void ChiefKnight_Declined_ChangesNothing()
    {
        var player = new RecordingNpcPlayer { Job = 1111, Level = 120 };
        Run(20408, 1101000, player, ending: false, accept: 0);
        Assert.Empty(player.JobChanges);
        Assert.Empty(player.CompletedQuests);
    }

    [Theory]
    [InlineData(20600, 1101002)]
    [InlineData(20610, 1101002)]
    [InlineData(20710, 1103002)]
    [InlineData(20720, 1101002)]
    public void Errands_AcceptStarts_DeclineDoesNot(int quest, int npc)
    {
        var yes = new RecordingNpcPlayer { Job = 1110, Level = 110 };
        Run(quest, npc, yes, ending: false, accept: 1);
        Assert.Equal(new[] { quest }, yes.StartedQuests);

        var no = new RecordingNpcPlayer { Job = 1110, Level = 110 };
        Run(quest, npc, no, ending: false, accept: 0);
        Assert.Empty(no.StartedQuests);
    }
}
