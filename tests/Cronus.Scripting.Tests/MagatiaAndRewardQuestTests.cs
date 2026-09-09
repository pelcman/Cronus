using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// Magatia society entry (3301 / 3303: the broker takes 30,000 / 50,000 meso as the JMS Check
/// dialogue says), the cape re-issues (3305 / 3306: explain and start), the alchemy lessons
/// (6030–6032: lecture, refuse under 10,000 meso — the fee itself is the quest Act), and the
/// single-NPC reward quests 2186 (glasses), 3452 (strap), 3833 (herb tiers), 3382 (marbles),
/// 2197 (monster book).
/// </summary>
public class MagatiaAndRewardQuestTests
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

    /// <summary>Drives one quest entry point to its end; plain lines get "next", yes/no and accept prompts the given answers.</summary>
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
    [InlineData(3301, 30000)]
    [InlineData(3303, 50000)]
    public void Broker_TakesTheFee_AndCompletes(int quest, int fee)
    {
        var player = new RecordingNpcPlayer { Meso = fee + 5 };
        Run(quest, 2111007, player, ending: true, yes: 1);
        Assert.Equal(5, player.Meso);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
    }

    [Theory]
    [InlineData(3301, 30000)]
    [InlineData(3303, 50000)]
    public void Broker_ShortOfMeso_OrDeclined_KeepsTheMeso(int quest, int fee)
    {
        var poor = new RecordingNpcPlayer { Meso = fee - 1 };
        Run(quest, 2111007, poor, ending: true);
        Assert.Equal(fee - 1, poor.Meso);
        Assert.Empty(poor.CompletedQuests);

        var declined = new RecordingNpcPlayer { Meso = fee };
        Run(quest, 2111007, declined, ending: true, yes: 0);
        Assert.Equal(fee, declined.Meso);
        Assert.Empty(declined.CompletedQuests);
    }

    [Theory]
    [InlineData(3305, 2111000)]
    [InlineData(3306, 2111001)]
    public void CapeReissue_AcceptStarts_DeclineDoesNot(int quest, int npc)
    {
        var yes = new RecordingNpcPlayer();
        Run(quest, npc, yes, ending: false, accept: 1);
        Assert.Equal(new[] { quest }, yes.StartedQuests);

        var no = new RecordingNpcPlayer();
        Run(quest, npc, no, ending: false, accept: 0);
        Assert.Empty(no.StartedQuests);
    }

    [Theory]
    [InlineData(6030, 2111000)]
    [InlineData(6031, 2012017)]
    [InlineData(6032, 2110004)]
    public void Lesson_CompletesWithTheFeeInHand_RefusesWithout(int quest, int npc)
    {
        var rich = new RecordingNpcPlayer { Meso = 10000 };
        Run(quest, npc, rich, ending: true);
        Assert.Equal(new[] { quest }, rich.CompletedQuests);
        Assert.Equal(10000, rich.Meso); // the fee is the quest Act's job, not the script's

        var poor = new RecordingNpcPlayer { Meso = 9999 };
        Run(quest, npc, poor, ending: true);
        Assert.Empty(poor.CompletedQuests);
    }

    [Fact]
    public void AbelsGlasses_TakenAndRewarded()
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[4031853] = 1;
        Run(2186, 1094001, player, ending: true);
        Assert.Equal(0, player.Inventory[4031853]);
        Assert.Equal(10, player.Inventory[2030019]);
        Assert.Equal(1700, player.Exp);
        Assert.Equal(new[] { 2186 }, player.CompletedQuests);

        var without = new RecordingNpcPlayer();
        Run(2186, 1094001, without, ending: true);
        Assert.Empty(without.CompletedQuests);
    }

    [Fact]
    public void BlockpusStrap_TakenAndRewarded()
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[4000099] = 1;
        Run(3452, 2050001, player, ending: true);
        Assert.Equal(0, player.Inventory[4000099]);
        Assert.Equal(50, player.Inventory[2000011]);
        Assert.Equal(8000, player.Exp);
        Assert.Equal(new[] { 3452 }, player.CompletedQuests);
    }

    [Theory]
    [InlineData(1200, 200, 54000, 2040501, 1)]
    [InlineData(700, 100, 54000, 2020013, 50)]
    [InlineData(550, 50, 54000, 0, 0)]
    [InlineData(150, 50, 45000, 0, 0)]
    [InlineData(60, 10, 10000, 2020007, 50)]
    [InlineData(3, 2, 10, 2000000, 1)]
    public void HerbTiers_TakeTheTierAndPayIt(int have, int left, int exp, int bonusItem, int bonusCount)
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[4000294] = have;
        Run(3833, 2092000, player, ending: true);
        Assert.Equal(left, player.Inventory[4000294]);
        Assert.Equal(exp, player.Exp);
        if (bonusItem != 0)
        {
            Assert.Equal(bonusCount, player.Inventory[bonusItem]);
        }

        Assert.Equal(new[] { 3833 }, player.CompletedQuests);
    }

    [Fact]
    public void HerbTiers_WithNoHerb_IsRefused()
    {
        var player = new RecordingNpcPlayer();
        Run(3833, 2092000, player, ending: true);
        Assert.Empty(player.CompletedQuests);
        Assert.Equal(0, player.Exp);
    }

    [Theory]
    [InlineData(25, 25, false, 1122010, 0, 0)]
    [InlineData(25, 25, true, 2041212, 15, 15)]
    [InlineData(10, 12, false, 2041212, 0, 2)]
    public void Marbles_PayThePendantOrTheStone(int a, int b, bool hasPendant, int reward, int leftA, int leftB)
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[4001159] = a;
        player.Inventory[4001160] = b;
        if (hasPendant)
        {
            player.Inventory[1122010] = 1;
        }

        Run(3382, 2112014, player, ending: true);
        Assert.Equal(hasPendant && reward == 1122010 ? 2 : 1, player.Inventory[reward]);
        Assert.Equal(leftA, player.Inventory[4001159]);
        Assert.Equal(leftB, player.Inventory[4001160]);
        Assert.Equal(new[] { 3382 }, player.CompletedQuests);
    }

    [Fact]
    public void Marbles_TooFew_IsRefused()
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[4001159] = 9;
        player.Inventory[4001160] = 30;
        Run(3382, 2112014, player, ending: true);
        Assert.Empty(player.CompletedQuests);
        Assert.Equal(9, player.Inventory[4001159]);
    }

    [Fact]
    public void MonsterBook_OneLine_ThenCompletes()
    {
        var player = new RecordingNpcPlayer { Level = 5 };
        Run(2197, 2006, player, ending: true);
        Assert.Equal(new[] { 2197 }, player.CompletedQuests);
    }
}
