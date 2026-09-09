using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The job-hall portals: rankRoom sends each hall into its ranking room (all eight JMS v186 targets
/// verified), and tutorialNPC opens the hall's instructor for a level-10-or-under beginner — and
/// does nothing for anyone else.
/// </summary>
public class JobHallPortalsTests
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
    [InlineData(100000201, 100000204, 2)] // 弓使い学院 → 弓使いの殿堂
    [InlineData(101000003, 101000004, 2)] // 魔法図書館 → 魔法使いの殿堂
    [InlineData(102000003, 102000004, 1)] // 戦士の聖殿 → 戦士の殿堂
    [InlineData(103000003, 103000008, 1)] // 盗賊のアジト → 盗賊の殿堂
    [InlineData(120000101, 120000105, 1)] // 航海室 → 訓練場
    [InlineData(130000000, 130000100, 5)] // エレヴ → 騎士の殿堂
    [InlineData(130000200, 130000100, 4)] // エレヴの分かれ道 → 騎士の殿堂
    [InlineData(140010100, 140010110, 1)] // リエン修行場入口 → 英雄の殿堂
    public void RankRoom_WarpsEachHallIntoItsRankingRoom(int hall, int room, int portal)
    {
        var player = new RecordingNpcPlayer { MapId = hall };
        Engine().Run("rankRoom", player);
        Assert.Equal((room, portal), player.Warped);
    }

    [Fact]
    public void RankRoom_DoesNothingElsewhere()
    {
        var player = new RecordingNpcPlayer { MapId = 100000000 };
        Engine().Run("rankRoom", player);
        Assert.Null(player.Warped);
    }

    [Theory]
    [InlineData(100000201, 1012100)] // ヘレナ
    [InlineData(101000003, 1032001)] // ハインズ
    [InlineData(102000003, 1022000)]
    [InlineData(103000003, 1052001)] // ダークロード
    [InlineData(120000101, 1090000)] // カイリン
    public void TutorialNpc_OpensTheInstructor_ForALowLevelBeginner(int hall, int instructor)
    {
        var player = new RecordingNpcPlayer { MapId = hall, Level = 8 };
        Engine().Run("tutorialNPC", player);
        Assert.Equal(instructor, player.OpenedNpc);
    }

    [Theory]
    [InlineData(100000201, 30, 0)]   // too high a level
    [InlineData(100000201, 8, 100)]  // already a warrior
    [InlineData(100000000, 8, 0)]    // not a hall
    public void TutorialNpc_DoesNothing_Otherwise(int map, int level, int job)
    {
        var player = new RecordingNpcPlayer { MapId = map, Level = level, Job = job };
        Engine().Run("tutorialNPC", player);
        Assert.Null(player.OpenedNpc);
    }
}
