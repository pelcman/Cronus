using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The Maple Island / Victoria service NPCs: Shanks' ship (150 mesos, Lv7, free with Lucas'
/// letter), Robin's Q&amp;A, Sera and Hina, Rain, Jane's potions (after quest 2013), the statue into
/// the Forest of Patience, the Sleepywood sauna, Shane's forest entry, Arwen's alchemy and
/// Henkel's training ground.
/// </summary>
public class ServiceNpcTests
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

    /// <summary>Runs an NPC: plain lines get "next"; yes/no prompts take answers from <paramref name="yes"/>; menus take selections from <paramref name="menus"/> in order.</summary>
    private static void Run(int npc, RecordingNpcPlayer player, int[]? yes = null, int[]? menus = null)
    {
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(npc, dialog, player)!;
        int y = 0, m = 0;
        while (!cm.IsEnded && !cts.IsCancellationRequested)
        {
            if (dialog.TryTake(out int type, 20))
            {
                switch (type)
                {
                    case 2: cm.Advance(2, yes is not null && y < yes.Length ? yes[y++] : 1, -1, string.Empty); break;
                    case 5: cm.Advance(5, 1, menus is not null && m < menus.Length ? menus[m++] : 0, string.Empty); break;
                    default: cm.Advance(type, 1, -1, string.Empty); break;
                }
            }
        }

        Assert.True(cm.IsEnded, "conversation did not end");
    }

    [Fact]
    public void Shanks_TakesTheFare_AndSails()
    {
        var player = new RecordingNpcPlayer { Level = 8, Meso = 200, MapId = 2000000 };
        Run(22000, player, yes: new[] { 1 });
        Assert.Equal(50, player.Meso);
        Assert.Equal((104000000, 0), player.Warped);
    }

    [Fact]
    public void Shanks_LettersRideFree_LowLevelAndBrokeStay()
    {
        var letter = new RecordingNpcPlayer { Level = 3, Meso = 0 };
        letter.Inventory[4031801] = 1;
        Run(22000, letter, yes: new[] { 1 });
        Assert.Equal(0, letter.Inventory[4031801]);
        Assert.Equal((104000000, 0), letter.Warped);

        var low = new RecordingNpcPlayer { Level = 6, Meso = 500 };
        Run(22000, low, yes: new[] { 1 });
        Assert.Null(low.Warped);
        Assert.Equal(500, low.Meso);

        var broke = new RecordingNpcPlayer { Level = 10, Meso = 100 };
        Run(22000, broke, yes: new[] { 1 });
        Assert.Null(broke.Warped);

        var no = new RecordingNpcPlayer { Level = 10, Meso = 500 };
        Run(22000, no, yes: new[] { 0 });
        Assert.Null(no.Warped);
        Assert.Equal(500, no.Meso);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(8)]
    [InlineData(16)]
    public void Robin_AnswersEveryTopic(int topic)
    {
        var player = new RecordingNpcPlayer();
        Run(2003, player, menus: new[] { topic });
    }

    [Fact]
    public void SeraAndRain_JustTalk_HinaSendsYouOut()
    {
        Run(2100, new RecordingNpcPlayer());
        Run(12101, new RecordingNpcPlayer());

        var out1 = new RecordingNpcPlayer { MapId = 10000 };
        Run(2101, out1, yes: new[] { 1 });
        Assert.Equal((40000, 0), out1.Warped);

        var stay = new RecordingNpcPlayer { MapId = 10000 };
        Run(2101, stay, yes: new[] { 0 });
        Assert.Null(stay.Warped);
    }

    [Fact]
    public void Jane_SellsAfterHerLastChallenge()
    {
        var buyer = new RecordingNpcPlayer { Meso = 20000 };
        buyer.SetQuestDone(2013);
        Run(1002100, buyer, yes: new[] { 1 }, menus: new[] { 1, 2 }); // ウナギ焼 ×10
        Assert.Equal(10, buyer.Inventory[2022003]);
        Assert.Equal(20000 - 1060 * 10, buyer.Meso);

        var poor = new RecordingNpcPlayer { Meso = 100 };
        poor.SetQuestDone(2013);
        Run(1002100, poor, yes: new[] { 1 }, menus: new[] { 0, 0 });
        Assert.Equal(0, poor.Inventory.GetValueOrDefault(2000002));
        Assert.Equal(100, poor.Meso);

        var early = new RecordingNpcPlayer { Meso = 10000 };
        Run(1002100, early, yes: new[] { 1 }, menus: new[] { 0, 0 });
        Assert.Empty(early.Inventory);
    }

    [Theory]
    [InlineData(0, 0, null)]
    [InlineData(2052, 0, 105040310)]
    [InlineData(2053, 1, 105040312)]
    [InlineData(2054, 2, 105040314)]
    public void Statue_OpensTheForestAsFarAsZonesQuestsAllow(int quest, int pick, int? map)
    {
        var player = new RecordingNpcPlayer { MapId = 105040300 };
        if (quest != 0)
        {
            player.StartedQuests.Add(quest);
        }

        Run(1061006, player, menus: new[] { pick });
        Assert.Equal(map is null ? null : (map.Value, 0), player.Warped);
    }

    [Theory]
    [InlineData(0, 499, 105040401)]
    [InlineData(1, 999, 105040402)]
    public void Hotel_ChargesAndSendsToTheSauna(int pick, int cost, int map)
    {
        var player = new RecordingNpcPlayer { Meso = 1000, MapId = 105040400 };
        Run(1061100, player, yes: new[] { 1 }, menus: new[] { pick });
        Assert.Equal(1000 - cost, player.Meso);
        Assert.Equal((map, 0), player.Warped);

        var poor = new RecordingNpcPlayer { Meso = 400, MapId = 105040400 };
        Run(1061100, poor, yes: new[] { 1 }, menus: new[] { pick });
        Assert.Null(poor.Warped);
        Assert.Equal(400, poor.Meso);
    }

    [Theory]
    [InlineData(30, 0, 101000100)]
    [InlineData(55, 0, 101000102)]
    [InlineData(60, 2050, 101000100)]
    [InlineData(30, 2051, 101000102)]
    public void Shane_SellsTheForestByLevelOrQuest(int level, int quest, int map)
    {
        var player = new RecordingNpcPlayer { Level = level, Meso = 6000 };
        if (quest != 0)
        {
            player.StartedQuests.Add(quest);
        }

        Run(1032003, player, yes: new[] { 1 });
        Assert.Equal((map, 0), player.Warped);
        Assert.Equal(1000, player.Meso);
    }

    [Fact]
    public void Shane_RefusesLowLevelsAndThePoor()
    {
        var low = new RecordingNpcPlayer { Level = 20, Meso = 6000 };
        Run(1032003, low, yes: new[] { 1 });
        Assert.Null(low.Warped);

        var poor = new RecordingNpcPlayer { Level = 30, Meso = 100 };
        Run(1032003, poor, yes: new[] { 1 });
        Assert.Null(poor.Warped);
        Assert.Equal(100, poor.Meso);
    }

    [Fact]
    public void Arwen_MakesTheMoonRock_FromSevenPlatesAndTenThousand()
    {
        var player = new RecordingNpcPlayer { Level = 45, Meso = 12000 };
        for (int i = 4011000; i <= 4011006; i++) player.Inventory[i] = 1;
        Run(1032100, player, yes: new[] { 1 }, menus: new[] { 0 });
        Assert.Equal(1, player.Inventory[4011007]);
        Assert.Equal(2000, player.Meso);
        Assert.All(Enumerable.Range(4011000, 7), i => Assert.Equal(0, player.Inventory[i]));
    }

    [Fact]
    public void Arwen_MakesTheStarRock_AndTheBlackFeather()
    {
        var star = new RecordingNpcPlayer { Level = 45, Meso = 15000 };
        for (int i = 4021000; i <= 4021008; i++) star.Inventory[i] = 1;
        Run(1032100, star, yes: new[] { 1 }, menus: new[] { 1 });
        Assert.Equal(1, star.Inventory[4021009]);
        Assert.Equal(0, star.Meso);

        var feather = new RecordingNpcPlayer { Level = 45, Meso = 30000 };
        feather.Inventory[4001006] = 1;
        feather.Inventory[4011007] = 1;
        feather.Inventory[4021008] = 1;
        Run(1032100, feather, yes: new[] { 1 }, menus: new[] { 2 });
        Assert.Equal(1, feather.Inventory[4031042]);
        Assert.Equal(0, feather.Inventory[4011007]);
        Assert.Equal(0, feather.Meso);
    }

    [Fact]
    public void Arwen_RefusesWithoutMaterials_OrUnderForty()
    {
        var missing = new RecordingNpcPlayer { Level = 45, Meso = 12000 };
        Run(1032100, missing, yes: new[] { 1 }, menus: new[] { 0 });
        Assert.Equal(0, missing.Inventory.GetValueOrDefault(4011007));
        Assert.Equal(12000, missing.Meso);

        var young = new RecordingNpcPlayer { Level = 30, Meso = 12000 };
        Run(1032100, young, yes: new[] { 1 }, menus: new[] { 0 });
        Assert.Equal(12000, young.Meso);
    }

    [Fact]
    public void Henkel_TrainsTheYoung()
    {
        var kid = new RecordingNpcPlayer { Level = 12 };
        Run(1012119, kid, yes: new[] { 1 });
        Assert.Equal((910060000, 0), kid.Warped);

        var special = new RecordingNpcPlayer { Level = 12 };
        special.StartedQuests.Add(22516);
        Run(1012119, special, yes: new[] { 1 });
        Assert.Equal((910060100, 0), special.Warped);

        var grown = new RecordingNpcPlayer { Level = 20 };
        Run(1012119, grown, yes: new[] { 1 });
        Assert.Null(grown.Warped);
    }
}
