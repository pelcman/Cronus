using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The plain and gated entry portals ported from Cosmic and checked against JMS v186 map data:
/// straight warps (Nix forests, Pianus, Eliza's garden, …), the Aran mirror cave pair, the
/// pyramid's remembered return, and the quest / item / hour gated ones (Rien, Mai's training,
/// the cursed forest, the Nautilus cow shed, the dark witch's cave, warp cards, Tristan, Ariant).
/// </summary>
public class EntryPortalTests
{
    private static PortalScriptEngine Engine()
    {
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scripts", "portal")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        return new PortalScriptEngine(new FolderPortalScriptSource(Path.Combine(root, "scripts", "portal")));
    }

    [Theory]
    [InlineData("eliza_Garden", 200010300, 920020000, 2)]
    [InlineData("balogTemple", 105090200, 105100000, 2)]
    [InlineData("catPriest_map", 250010504, 925000000, 2)]
    [InlineData("enterWarehouse", 300000010, 300000011, 0)]
    [InlineData("subway_in2", 103000100, 103000101, 3)]
    [InlineData("nets_in", 260020500, 926010000, 4)]
    public void PlainWarps_ByPortalIndex(string script, int from, int to, int portal)
    {
        var player = new RecordingNpcPlayer { MapId = from };
        Engine().Run(script, player);
        Assert.Equal((to, portal), player.Warped);
    }

    [Theory]
    [InlineData("Pianus", 230040410, 230040420, "out00")]
    [InlineData("enterAchter", 100000200, 100000201, "out02")]
    [InlineData("inNix1", 240020101, 240020600, "out00")]
    [InlineData("inNix2", 240020402, 240020600, "out01")]
    [InlineData("outNix1", 240020600, 240020101, "in00")]
    [InlineData("outNix2", 240020600, 240020401, "in00")]
    [InlineData("mayong", 240020400, 240020401, "out00")]
    [InlineData("gryphius", 240020100, 240020101, "out00")]
    [InlineData("minar_job4", 240010500, 240010501, "out00")]
    [InlineData("dracoout", 240000110, 240000100, "east00")]
    [InlineData("move_elin", 222020400, 300000100, "out00")]
    [InlineData("moveNext", 108000700, 108000710, "east00")]
    [InlineData("moveBefore", 108000710, 108000700, "west00")]
    public void PlainWarps_ByPortalName(string script, int from, int to, string portal)
    {
        var player = new RecordingNpcPlayer { MapId = from };
        Engine().Run(script, player);
        Assert.Equal((to, portal), player.WarpedNamed);
    }

    [Fact]
    public void Pyramid_RemembersTheWayIn_AndReturnsToIt()
    {
        var player = new RecordingNpcPlayer { MapId = 260020500 };
        Engine().Run("nets_in", player);
        Assert.True(player.Remembered);

        player.MapId = 926010000;
        Engine().Run("nets_out", player);
        Assert.Equal(260020500, player.RememberedFallback);
    }

    [Theory]
    [InlineData(2000, false, "st00")]
    [InlineData(2000, true, "west00")]
    [InlineData(2100, false, "west00")]
    public void RienFirst_NewLegendsLandAtTheStart(int job, bool lilinDone, string portal)
    {
        var player = new RecordingNpcPlayer { MapId = 140010000, Job = job };
        if (lilinDone)
        {
            player.SetQuestDone(21014);
        }

        Engine().Run("enterRienFirst", player);
        Assert.Equal((140000000, portal), player.WarpedNamed);
    }

    [Theory]
    [InlineData("enterGym", 21701, 914010000)]
    [InlineData("enterGym", 21702, 914010100)]
    [InlineData("enterGym", 21703, 914010200)]
    [InlineData("entertraining", 1041, 1010100)]
    [InlineData("entertraining", 1042, 1010200)]
    [InlineData("entertraining", 1043, 1010300)]
    [InlineData("entertraining", 1044, 1010400)]
    public void TrainingGrounds_OpenForTheActiveStage(string script, int quest, int map)
    {
        var player = new RecordingNpcPlayer();
        player.StartedQuests.Add(quest);
        Engine().Run(script, player);
        Assert.Equal(map, player.Warped?.Map);
    }

    [Theory]
    [InlineData("enterGym")]
    [InlineData("entertraining")]
    public void TrainingGrounds_RefuseOutsiders(string script)
    {
        var player = new RecordingNpcPlayer();
        Engine().Run(script, player);
        Assert.Null(player.Warped);
        Assert.Single(player.Messages);
    }

    [Theory]
    [InlineData(false, false, 18, null)]
    [InlineData(true, false, 12, null)]
    [InlineData(true, false, 18, 910100000)]
    [InlineData(true, false, 3, 910100000)]
    [InlineData(false, true, 23, 910100001)]
    public void CursedForest_NeedsTheQuestAndTheNight(bool started, bool faustDone, int hour, int? map)
    {
        var player = new RecordingNpcPlayer { MapId = 100040105, Hour = hour };
        if (started)
        {
            player.StartedQuests.Add(2224);
        }

        if (faustDone)
        {
            player.SetQuestDone(2227);
        }

        Engine().Run("curseforest", player);
        if (map is null)
        {
            Assert.Null(player.WarpedNamed);
            Assert.Single(player.Messages);
        }
        else
        {
            Assert.Equal((map.Value, "out00"), player.WarpedNamed);
        }
    }

    [Fact]
    public void CowShed_LetsOnlyAFullBottleOut_DuringTheQuest()
    {
        var half = new RecordingNpcPlayer { MapId = 912000100 };
        half.StartedQuests.Add(2180);
        half.Inventory[4031848] = 1;
        Engine().Run("end_cow", half);
        Assert.Null(half.Warped);
        Assert.Single(half.Messages);

        var full = new RecordingNpcPlayer { MapId = 912000100 };
        full.StartedQuests.Add(2180);
        full.Inventory[4031850] = 1;
        Engine().Run("end_cow", full);
        Assert.Equal((120000103, 0), full.Warped);

        var visitor = new RecordingNpcPlayer { MapId = 912000100 };
        Engine().Run("end_cow", visitor);
        Assert.Equal((120000103, 0), visitor.Warped);
    }

    [Theory]
    [InlineData(false, false, false, null)]
    [InlineData(true, false, false, 924010000)]
    [InlineData(true, true, false, 924010100)]
    [InlineData(true, true, true, 924010200)]
    public void WitchCave_DeepensWithTheStory(bool eggs, bool knight, bool curse, int? map)
    {
        var player = new RecordingNpcPlayer { MapId = 240040510 };
        if (eggs) player.SetQuestDone(20404);
        if (knight) player.SetQuestDone(20406);
        if (curse) player.SetQuestDone(20407);
        Engine().Run("enterWitch", player);
        if (map is null)
        {
            Assert.Null(player.Warped);
            Assert.Single(player.Messages);
        }
        else
        {
            Assert.Equal((map.Value, 1), player.Warped);
        }
    }

    [Theory]
    [InlineData("enter_earth00", 120000101, 221000300, "earth00")]
    [InlineData("enter_earth01", 221000300, 120000101, "earth01")]
    public void WarpCardPortals_NeedTheCard(string script, int from, int to, string portal)
    {
        var holder = new RecordingNpcPlayer { MapId = from };
        holder.Inventory[4031890] = 1;
        Engine().Run(script, holder);
        Assert.Equal((to, portal), holder.WarpedNamed);

        var none = new RecordingNpcPlayer { MapId = from };
        Engine().Run(script, none);
        Assert.Null(none.WarpedNamed);
        Assert.Single(none.Messages);
    }

    [Fact]
    public void Tristan_OnlyAfterTheMemo()
    {
        var before = new RecordingNpcPlayer { MapId = 105100100 };
        Engine().Run("tristanEnter", before);
        Assert.Null(before.WarpedNamed);

        var after = new RecordingNpcPlayer { MapId = 105100100 };
        after.SetQuestDone(2238);
        Engine().Run("tristanEnter", after);
        Assert.Equal((105100101, "in00"), after.WarpedNamed);
    }

    [Fact]
    public void PigFarm_OnlyOnCamillasErrand()
    {
        var errand = new RecordingNpcPlayer { MapId = 100030000 };
        errand.StartedQuests.Add(2073);
        Engine().Run("q2073", errand);
        Assert.Equal((900000000, 0), errand.Warped);

        var stranger = new RecordingNpcPlayer { MapId = 100030000 };
        Engine().Run("q2073", stranger);
        Assert.Null(stranger.Warped);
        Assert.Single(stranger.Messages);
    }

    [Fact]
    public void AriantPalaceAndHideout_AreGated()
    {
        var pass = new RecordingNpcPlayer { MapId = 260000300 };
        pass.Inventory[4031582] = 1;
        Engine().Run("ariant_castle", pass);
        Assert.Equal((260000301, 5), pass.Warped);

        var noPass = new RecordingNpcPlayer { MapId = 260000300 };
        Engine().Run("ariant_castle", noPass);
        Assert.Null(noPass.Warped);

        var bandit = new RecordingNpcPlayer { MapId = 260000200 };
        bandit.SetQuestDone(3928, 3931, 3934);
        Engine().Run("ariant_Agit", bandit);
        Assert.Equal((260000201, 1), bandit.Warped);

        var outsider = new RecordingNpcPlayer { MapId = 260000200 };
        outsider.SetQuestDone(3928, 3931);
        Engine().Run("ariant_Agit", outsider);
        Assert.Null(outsider.Warped);
    }

    [Fact]
    public void FoxHill_TailAndQuestOpenTheColdForest()
    {
        var hunter = new RecordingNpcPlayer { MapId = 222010300 };
        hunter.StartedQuests.Add(3647);
        hunter.Inventory[4031793] = 1;
        Engine().Run("foxLaidy_map", hunter);
        Assert.Equal((922220000, "east00"), hunter.WarpedNamed);

        var other = new RecordingNpcPlayer { MapId = 222010300 };
        Engine().Run("foxLaidy_map", other);
        Assert.Equal((222010200, "east00"), other.WarpedNamed);
    }

    [Fact]
    public void LudiSecretPassage_DropsTheMachineParts()
    {
        var player = new RecordingNpcPlayer { MapId = 922000009 };
        player.Inventory[4031092] = 5;
        Engine().Run("ludi021", player);
        Assert.Equal(0, player.Inventory[4031092]);
        Assert.Equal((220020600, 0), player.Warped);
    }
}
