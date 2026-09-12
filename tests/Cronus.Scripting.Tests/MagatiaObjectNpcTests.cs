using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// Magatia's story objects (Kason's and Humanoid A's quest warps, the bookshelf, the wall, the
/// frame, the desk), the Eos rocks, the Vicious Plant, the 4th-job trial objects (紅葉玉, 古代氷石,
/// 生命の泉) and Alcaster's post-quest shop, plus the one-line townsfolk.
/// </summary>
public class MagatiaObjectNpcTests
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
    [InlineData(2111001)]
    [InlineData(2111004)]
    [InlineData(2111005)]
    [InlineData(2111007)]
    [InlineData(2111008)]
    [InlineData(2111009)]
    [InlineData(2041024)]
    [InlineData(2041029)]
    [InlineData(2012012)]
    [InlineData(2120003)]
    [InlineData(2040031)]
    public void Townsfolk_SayOneLine(int npc)
    {
        Assert.Equal(1, Run(npc, new RecordingNpcPlayer()));
    }

    [Theory]
    [InlineData(2111000, 3310, 4031709, 926120100)]
    [InlineData(2111003, 3335, 4031695, 926120300)]
    public void QuestWarps_OnlyWhileTheItemIsStillMissing(int npc, int quest, int item, int map)
    {
        var seeker = new RecordingNpcPlayer();
        seeker.StartedQuests.Add(quest);
        Run(npc, seeker);
        Assert.Equal((map, "out00"), seeker.WarpedNamed);

        var holder = new RecordingNpcPlayer();
        holder.StartedQuests.Add(quest);
        holder.Inventory[item] = 1;
        Run(npc, holder);
        Assert.Null(holder.WarpedNamed);

        var stranger = new RecordingNpcPlayer();
        Run(npc, stranger);
        Assert.Null(stranger.WarpedNamed);
    }

    [Theory]
    [InlineData(2111010, 3309, 4031708)]
    [InlineData(2111013, 3322, 4031697)]
    public void HiddenItems_FoundOnceDuringTheQuest(int npc, int quest, int item)
    {
        var player = new RecordingNpcPlayer();
        player.StartedQuests.Add(quest);
        Run(npc, player);
        Assert.Equal(1, player.Inventory[item]);
        Run(npc, player);
        Assert.Equal(1, player.Inventory[item]);

        var other = new RecordingNpcPlayer();
        Run(npc, other);
        Assert.Equal(0, other.Inventory.GetValueOrDefault(item));
    }

    [Fact]
    public void Wall_MarksTheClue_WhenLookedAt()
    {
        var player = new RecordingNpcPlayer();
        player.StartedQuests.Add(3311);
        Run(2111011, player, yes: new[] { 1 });
        Assert.Equal("5", player.QuestData[3311]);

        var glance = new RecordingNpcPlayer();
        glance.StartedQuests.Add(3311);
        Run(2111011, glance, yes: new[] { 0 });
        Assert.Empty(glance.QuestData);

        Assert.Equal(0, Run(2111011, new RecordingNpcPlayer()));
        Assert.Equal(0, Run(2111014, new RecordingNpcPlayer()));
        Assert.Equal(1, Run(2111014, player));
    }

    [Fact]
    public void EosRocks_SpendAScroll()
    {
        var first = new RecordingNpcPlayer { MapId = 221024400 };
        first.Inventory[4001020] = 2;
        Run(2040024, first, yes: new[] { 1 });
        Assert.Equal(1, first.Inventory[4001020]);
        Assert.Equal((221022900, 3), first.Warped);

        var third = new RecordingNpcPlayer { MapId = 221021700 };
        third.Inventory[4001020] = 1;
        Run(2040026, third, yes: new[] { 1 }, menus: new[] { 1 });
        Assert.Equal(0, third.Inventory[4001020]);
        Assert.Equal((221020000, 4), third.Warped);

        var none = new RecordingNpcPlayer { MapId = 221024400 };
        Run(2040024, none, yes: new[] { 1 });
        Assert.Null(none.Warped);
    }

    [Fact]
    public void ViciousPlant_SendsYouBack()
    {
        var player = new RecordingNpcPlayer { MapId = 922020300 };
        Run(2043000, player);
        Assert.Equal((220080000, 0), player.Warped);
    }

    [Theory]
    [InlineData(2012023, 4031476, 4031456)]
    [InlineData(2030014, 4031450, 2280011)]
    public void TrialObjects_ExchangeTheItem(int npc, int from, int to)
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[from] = 1;
        Run(npc, player);
        Assert.Equal(0, player.Inventory[from]);
        Assert.Equal(1, player.Inventory[to]);

        var without = new RecordingNpcPlayer();
        Run(npc, without);
        Assert.Equal(0, without.Inventory.GetValueOrDefault(to));
    }

    [Fact]
    public void Fountain_FillsTheChaliceDuringTheQuest()
    {
        var player = new RecordingNpcPlayer();
        player.StartedQuests.Add(6280);
        player.Inventory[4031454] = 1;
        Run(2083005, player);
        Assert.Equal(0, player.Inventory[4031454]);
        Assert.Equal(1, player.Inventory[4031455]);

        var idle = new RecordingNpcPlayer();
        idle.Inventory[4031454] = 1;
        Run(2083005, idle);
        Assert.Equal(1, idle.Inventory[4031454]);
    }

    [Fact]
    public void Alcaster_SellsAfterTheCrystalQuest()
    {
        var buyer = new RecordingNpcPlayer { Meso = 30000 };
        buyer.SetQuestDone(3035);
        Run(2020005, buyer, yes: new[] { 1 }, menus: new[] { 2, 1 }); // 魔法の石 ×5
        Assert.Equal(5, buyer.Inventory[4006000]);
        Assert.Equal(5000, buyer.Meso);

        var early = new RecordingNpcPlayer { Meso = 30000 };
        Run(2020005, early, yes: new[] { 1 }, menus: new[] { 0, 0 });
        Assert.Empty(early.Inventory);
        Assert.Equal(30000, early.Meso);
    }
}
