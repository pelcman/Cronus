using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The self-contained Aran storyline quests: the practice pole arm (21700), Puo's last training
/// and the remembered skills (21703 Combo Ability, 21720 Booster, 21734 Combo Drain, 21740 Combo
/// Smash, 21748 Final Charge — first level handed over through teachSkill), the information
/// legs (21712 / 21716 / 21729 / 21736 / 21741 / 21753), the seal stones (21735 / 21739 / 21749 /
/// 21750 / 21754 / 21757), Spiruna (21738) and the Lith Harbor box (21766 / 21767).
/// </summary>
public class AranStoryQuestTests
{
    private const int Lilin = 1201000;
    private const int Tru = 1002104;
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

    private static RecordingNpcPlayer Aran(int level = 30) => new() { Job = 2100, Level = level };

    [Fact]
    public void NewBeginning_HandsThePracticePoleArm_AndStarts()
    {
        var player = Aran(10);
        Run(21700, Lilin, player, ending: false, accept: 1);
        Assert.Equal(1, player.Inventory[1442077]);
        Assert.Equal(new[] { 21700 }, player.StartedQuests);

        var declined = Aran(10);
        Run(21700, Lilin, declined, ending: false, accept: 0);
        Assert.Equal(0, declined.Inventory.GetValueOrDefault(1442077));
        Assert.Empty(declined.StartedQuests);
    }

    [Fact]
    public void LastTraining_StartsOnAccept_EndRemembersComboAbility()
    {
        var player = Aran(10);
        Run(21703, 1202006, player, ending: false, accept: 1);
        Assert.Equal(new[] { 21703 }, player.StartedQuests);

        Run(21703, 1202006, player, ending: true, yes: 1);
        Assert.Equal(new[] { 21703 }, player.CompletedQuests);
        Assert.Equal(2800, player.Exp);
        Assert.Equal(1, player.Skills[21000000]);
    }

    [Fact]
    public void RememberedSkill_IsNotLoweredWhenAlreadyKnown()
    {
        var player = Aran(10);
        player.Skills[21000000] = 5;
        Run(21703, 1202006, player, ending: true, yes: 1);
        Assert.Equal(5, player.Skills[21000000]);
        Assert.Equal(new[] { 21703 }, player.CompletedQuests);
    }

    [Theory]
    [InlineData(21720, 1201000, 21001003, 3900)]
    [InlineData(21734, 1002104, 21100005, 12500)]
    [InlineData(21740, 1201000, 21100004, 0)]
    [InlineData(21748, 1201000, 21100002, 20000)]
    public void StoryEnds_RememberASkill(int quest, int npc, int skill, int exp)
    {
        var player = Aran(60);
        Run(quest, npc, player, ending: true, yes: 1);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
        Assert.Equal(1, player.Skills[skill]);
        Assert.Equal(exp, player.Exp);
    }

    [Fact]
    public void PuppeteersWarning_Declined_ChangesNothing()
    {
        var player = Aran(22);
        Run(21720, Lilin, player, ending: true, yes: 0);
        Assert.Empty(player.CompletedQuests);
        Assert.Empty(player.Skills);
    }

    [Theory]
    [InlineData(21712, 1012111)]
    [InlineData(21716, 1032101)]
    [InlineData(21736, 1002104)]
    [InlineData(21738, 2032001)]
    [InlineData(21741, 1002104)]
    public void InformationLegs_AcceptStarts_DeclineDoesNot(int quest, int npc)
    {
        var yes = Aran(50);
        Run(quest, npc, yes, ending: false, accept: 1);
        Assert.Equal(new[] { quest }, yes.StartedQuests);

        var no = Aran(50);
        Run(quest, npc, no, ending: false, accept: 0);
        Assert.Empty(no.StartedQuests);
    }

    [Theory]
    [InlineData(21729, 1061019)]
    [InlineData(21734, 1002104)]
    [InlineData(21735, 1002104)]
    [InlineData(21740, 1002104)]
    [InlineData(21753, 2131000)]
    [InlineData(21766, 1002001)]
    public void PlainStarts_Start(int quest, int npc)
    {
        var player = Aran(60);
        Run(quest, npc, player, ending: false);
        Assert.Equal(new[] { quest }, player.StartedQuests);
    }

    [Fact]
    public void SealStone_HandedToLilin_OnlyWhenCarried()
    {
        var player = Aran(40);
        Run(21735, Lilin, player, ending: true);
        Assert.Empty(player.CompletedQuests);

        player.Inventory[4032323] = 1;
        Run(21735, Lilin, player, ending: true);
        Assert.Equal(0, player.Inventory[4032323]);
        Assert.Equal(6037, player.Exp);
        Assert.Equal(new[] { 21735 }, player.CompletedQuests);
    }

    [Theory]
    [InlineData(21739, 2032001, 29500)]
    [InlineData(21750, 2131000, 0)]
    [InlineData(21757, 1101002, 0)]
    [InlineData(21766, 1002001, 200)]
    public void PlainEnds_Complete(int quest, int npc, int exp)
    {
        var player = Aran(70);
        Run(quest, npc, player, ending: true);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
        Assert.Equal(exp, player.Exp);
    }

    [Theory]
    [InlineData(21704, 1201000, 500)]
    [InlineData(21749, 1002104, 0)]
    public void OnTheSpot_StartAndComplete(int quest, int npc, int exp)
    {
        var player = Aran(70);
        Run(quest, npc, player, ending: false);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
        Assert.Equal(exp, player.Exp);
    }

    [Theory]
    [InlineData(21754, 1012100, 4032328)]
    [InlineData(21767, 1204033, 4032423)]
    public void LetterAndBox_HandOutTheItemOnce(int quest, int npc, int item)
    {
        var player = Aran(63);
        Run(quest, npc, player, ending: false);
        Assert.Equal(1, player.Inventory[item]);
        Assert.Equal(new[] { quest }, player.StartedQuests);

        var holding = Aran(63);
        holding.Inventory[item] = 1;
        Run(quest, npc, holding, ending: false);
        Assert.Equal(1, holding.Inventory[item]);
    }
}
