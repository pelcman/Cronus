using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// キノコ王国 entry: the eleven instructor quests 2300–2310 (the letter 4032375, a warp to 106020000
/// on "I know the way", the guard captain 1300005 takes the letter, pays 6,000 exp and opens
/// 勇者の試験 2312), the JMS map-enter title effect, the forest investigation flags (2314 / 2322 /
/// 2324 via the セルフ 1300014 self-talk), the thorn gates, James (2325 / 2327) and the spare
/// spray (2338). Drives the shipped scripts/{map,quest,portal,npc} files.
/// </summary>
public class MushroomKingdomEntryTests
{
    private const int Guard = 1300005;
    private const int Self = 1300014;
    private const int Letter = 4032375;
    private const int Remover = 2430015;
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

    private static string RepoRoot()
    {
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scripts", "quest")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        return root;
    }

    private static NpcScriptEngine Engine() => new(
        new FolderNpcScriptSource(Path.Combine(RepoRoot(), "scripts", "npc")),
        new FolderNpcScriptSource(Path.Combine(RepoRoot(), "scripts", "quest")));

    private static PortalScriptEngine Scripts(string kind)
        => new(new FolderPortalScriptSource(Path.Combine(RepoRoot(), "scripts", kind)));

    /// <summary>Drives a conversation to its end: plain lines get "next"; accept and yes/no prompts get the given answers.</summary>
    private static void Drive(NpcConversation cm, Dialog dialog, int accept = 1, int yes = 1)
    {
        using var cts = new CancellationTokenSource(Timeout);
        while (!cm.IsEnded && !cts.IsCancellationRequested)
        {
            if (!dialog.TryTake(out int type, 20))
            {
                continue;
            }

            int action = type switch { 13 => accept, 2 => yes, _ => 1 };
            cm.Advance(type, action, -1, string.Empty);
        }

        Assert.True(cm.IsEnded, "conversation did not end");
    }

    private static void RunQuest(int quest, int npc, RecordingNpcPlayer player, bool ending, int accept = 1, int yes = 1)
    {
        var dialog = new Dialog();
        Drive(Engine().StartQuest(quest, npc, dialog, player, ending)!, dialog, accept, yes);
    }

    private static void RunNpc(int npc, RecordingNpcPlayer player)
    {
        var dialog = new Dialog();
        Drive(Engine().Start(npc, dialog, player)!, dialog);
    }

    public static IEnumerable<object[]> Instructors() => new[]
    {
        new object[] { 2300, 1022000 }, // コブシを開いて立て (warriors)
        new object[] { 2301, 1032001 }, // ハインズ (magicians)
        new object[] { 2302, 1052001 }, // ダークロード (thieves)
        new object[] { 2303, 1012100 }, // ヘレナ (archers)
        new object[] { 2304, 1090000 }, // カイリン (pirates)
        new object[] { 2305, 1101003 }, // ミハエル
        new object[] { 2306, 1101004 }, // オズ
        new object[] { 2307, 1101005 }, // イリーナ
        new object[] { 2308, 1101006 }, // イカルト
        new object[] { 2309, 1101007 }, // ホークアイ
        new object[] { 2310, 1201000 }, // リリン
    };

    [Theory]
    [MemberData(nameof(Instructors))]
    public void Instructor_AcceptAndKnowTheWay_GivesTheLetter_StartsAndWarps(int quest, int npc)
    {
        var player = new RecordingNpcPlayer();
        RunQuest(quest, npc, player, ending: false, accept: 1, yes: 1);
        Assert.Equal(1, player.Inventory[Letter]);
        Assert.Equal(new[] { quest }, player.StartedQuests);
        Assert.Equal((106020000, 0), player.Warped);
    }

    [Theory]
    [MemberData(nameof(Instructors))]
    public void Instructor_AcceptButAskTheWay_GivesTheLetter_StartsWithoutWarping(int quest, int npc)
    {
        var player = new RecordingNpcPlayer();
        RunQuest(quest, npc, player, ending: false, accept: 1, yes: 0);
        Assert.Equal(1, player.Inventory[Letter]);
        Assert.Equal(new[] { quest }, player.StartedQuests);
        Assert.Null(player.Warped);
    }

    [Fact]
    public void Instructor_Decline_ChangesNothing()
    {
        var player = new RecordingNpcPlayer();
        RunQuest(2300, 1022000, player, ending: false, accept: 0);
        Assert.Empty(player.StartedQuests);
        Assert.Equal(0, player.Inventory.GetValueOrDefault(Letter));
        Assert.Null(player.Warped);
    }

    [Fact]
    public void Instructor_DoesNotHandOutASecondLetter()
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[Letter] = 1;
        RunQuest(2300, 1022000, player, ending: false);
        Assert.Equal(1, player.Inventory[Letter]);
    }

    [Theory]
    [MemberData(nameof(Instructors))]
    public void GuardCaptain_WithTheLetter_TakesIt_PaysExp_CompletesAndOpensTheTrial(int quest, int npc)
    {
        _ = npc;
        var player = new RecordingNpcPlayer();
        player.Inventory[Letter] = 1;
        RunQuest(quest, Guard, player, ending: true);
        Assert.Equal(0, player.Inventory[Letter]);
        Assert.Equal(6000, player.Exp);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
        Assert.Equal(new[] { 2312 }, player.StartedQuests);
    }

    [Fact]
    public void GuardCaptain_WithoutTheLetter_IsRefused()
    {
        var player = new RecordingNpcPlayer();
        RunQuest(2300, Guard, player, ending: true);
        Assert.Empty(player.CompletedQuests);
        Assert.Empty(player.StartedQuests);
        Assert.Equal(0, player.Exp);
    }

    [Fact]
    public void EnteringTheTownCorner_ShowsTheThemedDungeonTitle()
    {
        var player = new RecordingNpcPlayer { MapId = 106020000 };
        Scripts("map").Run("TD_MC_title", player);
        Assert.Equal(new[] { "temaD/enter/mushCatle" }, player.ScreenEffects);
    }

    [Theory]
    [InlineData("investigate1")]
    [InlineData("investigate2")]
    public void InvestigationPoints_OpenTheSelfTalk(string portal)
    {
        var player = new RecordingNpcPlayer();
        Scripts("portal").Run(portal, player);
        Assert.Equal(Self, player.OpenedNpc);
        Assert.Null(player.Warped);
    }

    [Fact]
    public void SelfTalk_AtTheForestBarrier_FlagsTheFirstInvestigation()
    {
        var player = new RecordingNpcPlayer { MapId = 106020300 };
        player.StartedQuests.Add(2314);
        RunNpc(Self, player);
        Assert.Equal("1", player.QuestData[2314]);
    }

    [Fact]
    public void SelfTalk_AtTheForestBarrier_WithoutTheQuest_FlagsNothing()
    {
        var player = new RecordingNpcPlayer { MapId = 106020300 };
        RunNpc(Self, player);
        Assert.Empty(player.QuestData);
    }

    [Theory]
    [InlineData(true, false, 106020400)]
    [InlineData(false, true, 106020400)]
    [InlineData(false, false, 0)]
    public void ThornBarrier_OpensOnceTheSprayQuestLineReachesTheWall(bool started, bool done, int expectedMap)
    {
        var player = new RecordingNpcPlayer { MapId = 106020300 };
        if (started)
        {
            player.StartedQuests.Add(2321);
        }

        if (done)
        {
            player.SetQuestDone(2321);
        }

        Scripts("portal").Run("obstacle", player);
        if (expectedMap == 0)
        {
            Assert.Null(player.Warped);
            Assert.Equal(Self, player.OpenedNpc);
        }
        else
        {
            Assert.Equal((expectedMap, 0), player.Warped);
        }
    }

    [Theory]
    [InlineData(false, null, 106020500)]
    [InlineData(false, "1", 106020501)]
    [InlineData(true, null, 106020501)]
    public void RoadToTheCastle_ReachesTheWall_OnlyAfterTheRemover(bool done, string? data, int expectedMap)
    {
        var player = new RecordingNpcPlayer { MapId = 106020400 };
        if (done)
        {
            player.SetQuestDone(2324);
        }

        if (data is not null)
        {
            player.QuestData[2324] = data;
        }

        Scripts("portal").Run("gotocastle", player);
        Assert.Equal((expectedMap, 0), player.Warped);
    }

    [Fact]
    public void SelfTalk_AtTheWall_FlagsTheSecondInvestigation()
    {
        var player = new RecordingNpcPlayer { MapId = 106020500 };
        player.StartedQuests.Add(2322);
        RunNpc(Self, player);
        Assert.Equal("1", player.QuestData[2322]);
    }

    [Fact]
    public void SelfTalk_AtTheWall_UsesTheRemover_AndFlagsTheWallQuest()
    {
        var player = new RecordingNpcPlayer { MapId = 106020500 };
        player.StartedQuests.Add(2324);
        player.Inventory[Remover] = 1;
        RunNpc(Self, player);
        Assert.Equal(0, player.Inventory[Remover]);
        Assert.Equal("1", player.QuestData[2324]);
    }

    [Fact]
    public void SelfTalk_AtTheWall_WithoutTheRemover_KeepsWaiting()
    {
        var player = new RecordingNpcPlayer { MapId = 106020500 };
        player.StartedQuests.Add(2324);
        RunNpc(Self, player);
        Assert.Empty(player.QuestData);
    }

    [Fact]
    public void James_Found_CompletesTheSearch()
    {
        var player = new RecordingNpcPlayer { MapId = 106021201 };
        RunQuest(2325, 1300008, player, ending: true);
        Assert.Equal(new[] { 2325 }, player.CompletedQuests);
    }

    [Fact]
    public void James_EscapePlan_StartsTheThirdPart()
    {
        var player = new RecordingNpcPlayer { MapId = 106021201 };
        RunQuest(2327, 1300008, player, ending: false);
        Assert.Equal(new[] { 2327 }, player.StartedQuests);
    }

    [Theory]
    [InlineData(1, 1, false)]
    [InlineData(0, 1, true)]
    public void SpareSpray_OnlyWhenTheSprayIsGone(int have, int after, bool completes)
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[2430014] = have;
        RunQuest(2338, 1300007, player, ending: false);
        Assert.Equal(after, player.Inventory[2430014]);
        Assert.Equal(completes ? new[] { 2338 } : Array.Empty<int>(), player.CompletedQuests);
    }
}
