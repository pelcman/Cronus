using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The Nautilus cow shed (Tangyoon, the mother cows that fill the bottle a third at a time and
/// refuse the same cow twice, the calves that drink it), Murat, the shiny rock, the trash cans,
/// the boxes hiding Abel's glasses, the Ellinia stump, the Showa bathhouse and hideout guide, the
/// Ariant shops and story objects, the Forest of Patience flower piles, and the one-liners.
/// </summary>
public class TownsfolkNpcTests
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
        while (!Directory.Exists(Path.Combine(root, "scripts", "npc")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        return new NpcScriptEngine(new FolderNpcScriptSource(Path.Combine(root, "scripts", "npc")));
    }

    private static int Run(int npc, RecordingNpcPlayer player, int[]? yes = null, int[]? menus = null)
    {
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(npc, dialog, player)!;
        int y = 0, m = 0, prompts = 0;
        while (!cm.IsEnded && !cts.IsCancellationRequested)
        {
            if (dialog.TryTake(out int type, 20))
            {
                prompts++;
                switch (type)
                {
                    case 2: cm.Advance(2, yes is not null && y < yes.Length ? yes[y++] : 1, -1, string.Empty); break;
                    case 5: cm.Advance(5, 1, menus is not null && m < menus.Length ? menus[m++] : 0, string.Empty); break;
                    default: cm.Advance(type, 1, -1, string.Empty); break;
                }
            }
        }

        Assert.True(cm.IsEnded, "conversation did not end");
        return prompts;
    }

    [Theory]
    [InlineData(1092015)]
    [InlineData(1094000)]
    [InlineData(1032110)]
    [InlineData(1052109)]
    [InlineData(2131000)]
    [InlineData(2131002)]
    [InlineData(2131004)]
    [InlineData(2101000)]
    [InlineData(2101004)]
    [InlineData(2101008)]
    [InlineData(2101011)]
    public void Townsfolk_SayOneLine(int npc)
    {
        Assert.Equal(1, Run(npc, new RecordingNpcPlayer()));
    }

    [Fact]
    public void Tangyoon_SendsTheQuesterToTheShedWithAnEmptyBottle()
    {
        var quester = new RecordingNpcPlayer { MapId = 120000103 };
        quester.StartedQuests.Add(2180);
        Run(1092000, quester);
        Assert.Equal(1, quester.Inventory[4031847]);
        Assert.Equal((912000100, 0), quester.Warped);

        var other = new RecordingNpcPlayer { MapId = 120000103 };
        Run(1092000, other);
        Assert.Null(other.Warped);
        Assert.Empty(other.Inventory);
    }

    [Fact]
    public void Cows_FillTheBottleAThirdAtATime_AndRefuseTheSameCowTwice()
    {
        var player = new RecordingNpcPlayer { MapId = 912000100 };
        player.StartedQuests.Add(2180);
        player.Inventory[4031847] = 1;

        Run(1092090, player);
        Assert.Equal(1, player.Inventory[4031848]);
        Assert.Equal("1", player.QuestData[2180]);

        Run(1092090, player); // same cow: nothing changes
        Assert.Equal(1, player.Inventory[4031848]);

        Run(1092091, player);
        Assert.Equal(1, player.Inventory[4031849]);
        Assert.Equal("2", player.QuestData[2180]);

        Run(1092090, player);
        Assert.Equal(1, player.Inventory[4031850]);
        Assert.Equal(0, player.Inventory[4031849]);
    }

    [Fact]
    public void Calf_DrinksTheMilk()
    {
        var player = new RecordingNpcPlayer { MapId = 912000100 };
        player.Inventory[4031849] = 1;
        Run(1092094, player);
        Assert.Equal(0, player.Inventory[4031849]);
        Assert.Equal(1, player.Inventory[4031847]);

        Run(1092095, player); // empty bottle: no interest
        Assert.Equal(1, player.Inventory[4031847]);
    }

    [Fact]
    public void Murat_HandsTheReturnScroll_DuringTheQuest()
    {
        var player = new RecordingNpcPlayer();
        player.StartedQuests.Add(2175);
        Run(1092007, player);
        Assert.Equal(1, player.Inventory[2030019]);
        Assert.Equal((100000006, 0), player.Warped);

        var other = new RecordingNpcPlayer();
        Run(1092007, other);
        Assert.Empty(other.Inventory);
    }

    [Fact]
    public void ShinyRock_CompletesTheQuest()
    {
        var player = new RecordingNpcPlayer();
        player.StartedQuests.Add(2166);
        Run(1092016, player);
        Assert.Equal(new[] { 2166 }, player.CompletedQuests);
    }

    [Theory]
    [InlineData(1092018, 0, 4031839)]
    [InlineData(1032111, 20716, 4032142)]
    [InlineData(1052111, 20710, 4032136)]
    [InlineData(2103002, 3923, 4031578)]
    public void Containers_YieldTheQuestItemOnce(int npc, int quest, int item)
    {
        var player = new RecordingNpcPlayer();
        if (quest != 0)
        {
            player.StartedQuests.Add(quest);
        }

        Run(npc, player);
        Assert.Equal(1, player.Inventory[item]);
        Run(npc, player);
        Assert.Equal(1, player.Inventory[item]);
    }

    [Fact]
    public void Boxes_HideOneOfThreeGlasses_DuringAbelsQuest()
    {
        var player = new RecordingNpcPlayer();
        player.StartedQuests.Add(2186);
        Run(1094004, player);
        Assert.Equal(1, player.Inventory.Count);
        Assert.Contains(player.Inventory.Keys.Single(), new[] { 4031853, 4031854, 4031855 });

        Run(1094005, player);
        Assert.Equal(1, player.Inventory.Count);

        var other = new RecordingNpcPlayer();
        Run(1094002, other);
        Assert.Empty(other.Inventory);
    }

    [Theory]
    [InlineData(0, 801000100)]
    [InlineData(1, 801000200)]
    public void Bathhouse_ChargesAndSplitsByGender(int gender, int map)
    {
        var player = new RecordingNpcPlayer { Meso = 500, Gender = gender };
        Run(9120003, player, yes: new[] { 1 });
        Assert.Equal(200, player.Meso);
        Assert.Equal((map, "out00"), player.WarpedNamed);

        var poor = new RecordingNpcPlayer { Meso = 100, Gender = gender };
        Run(9120003, poor, yes: new[] { 1 });
        Assert.Null(poor.WarpedNamed);
    }

    [Fact]
    public void ShowaGuides_TakeYouToTheHideoutAndBack()
    {
        var go = new RecordingNpcPlayer { MapId = 801000000 };
        Run(9120015, go, menus: new[] { 1 });
        Assert.Equal((801040000, "in00"), go.WarpedNamed);

        var info = new RecordingNpcPlayer { MapId = 801000000 };
        Run(9120015, info, menus: new[] { 0 });
        Assert.Null(info.WarpedNamed);

        var back = new RecordingNpcPlayer { MapId = 801040000 };
        Run(9120200, back, yes: new[] { 1 });
        Assert.Equal((801000000, 0), back.Warped);

        var after = new RecordingNpcPlayer { MapId = 801040101 };
        Run(9120203, after);
        Assert.Equal((801000000, 0), after.Warped);
    }

    [Theory]
    [InlineData(2100002)]
    [InlineData(2100003)]
    public void AriantShops_Open(int npc)
    {
        var player = new RecordingNpcPlayer();
        Run(npc, player);
        Assert.Equal(new[] { npc }, player.Shops);
    }

    [Fact]
    public void AriantObjects_MarkTheirQuests()
    {
        var wall = new RecordingNpcPlayer();
        wall.StartedQuests.Add(3927);
        Run(2103001, wall);
        Assert.Equal("1", wall.QuestData[3927]);

        var oasis = new RecordingNpcPlayer();
        oasis.StartedQuests.Add(3900);
        Run(2103000, oasis);
        Assert.Equal("5", oasis.QuestData[3900]);

        var idle = new RecordingNpcPlayer();
        Run(2103000, idle);
        Assert.Empty(idle.QuestData);
    }

    [Theory]
    [InlineData(1063000, 2052, 4031025, 10)]
    [InlineData(1063001, 2053, 4031026, 20)]
    [InlineData(1063002, 2054, 4031028, 30)]
    public void FlowerPiles_GiveTheQuestFlowers_OrAnOre_ThenSendYouBack(int npc, int quest, int flower, int count)
    {
        var quester = new RecordingNpcPlayer();
        quester.StartedQuests.Add(quest);
        Run(npc, quester);
        Assert.Equal(count, quester.Inventory[flower]);
        Assert.Equal((105040300, 0), quester.Warped);

        var visitor = new RecordingNpcPlayer();
        Run(npc, visitor);
        Assert.Single(visitor.Inventory);
        Assert.DoesNotContain(flower, visitor.Inventory.Keys);
        Assert.Equal((105040300, 0), visitor.Warped);
    }
}
