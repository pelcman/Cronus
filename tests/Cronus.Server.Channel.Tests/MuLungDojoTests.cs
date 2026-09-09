using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>The Mu Lung Dojo tables and map arithmetic (ported from Event_DojoAgent), pure.</summary>
public class MuLungDojoTests
{
    [Theory]
    [InlineData(925020100, 1)]   // solo stage 1, copy 0
    [InlineData(925020105, 1)]   // solo stage 1, copy 5
    [InlineData(925023800, 38)]  // solo stage 38
    [InlineData(925030100, 1)]   // party stage 1
    [InlineData(925033814, 38)]  // party stage 38, last copy
    public void StageOf_ReadsTheStageFromAFightingMap(int mapId, int stage)
    {
        Assert.True(MuLungDojo.IsStageMap(mapId));
        Assert.Equal(stage, MuLungDojo.StageOf(mapId));
    }

    [Theory]
    [InlineData(925020000)] // the hall
    [InlineData(925020001)] // the entrance
    [InlineData(925020002)] // the exit hall
    [InlineData(925020010)] // the tutorial map
    [InlineData(100000000)] // Henesys
    public void StageOf_IsZeroForNonFightingMaps(int mapId)
    {
        Assert.Equal(0, MuLungDojo.StageOf(mapId));
    }

    [Fact]
    public void StageMap_StaysInTheSameBaseAndCopy()
    {
        Assert.Equal(925020200, MuLungDojo.StageMap(925020100, 2));  // solo copy 0: stage 1 → 2
        Assert.Equal(925020305, MuLungDojo.StageMap(925020105, 3));  // solo copy 5
        Assert.Equal(925033807, MuLungDojo.StageMap(925033107, 38)); // party copy 7
    }

    [Fact]
    public void MobForStage_MatchesTheOraclesLadder_AndIsZeroOnRestingStages()
    {
        Assert.Equal(9300184, MuLungDojo.MobForStage(1));   // マノ
        Assert.Equal(9300188, MuLungDojo.MobForStage(5));   // 大王ムカデ
        Assert.Equal(9300210, MuLungDojo.MobForStage(32));  // クリムゾンバルログ
        Assert.Equal(9300215, MuLungDojo.MobForStage(38));  // 武功

        foreach (int resting in new[] { 6, 12, 18, 24, 30, 36 })
        {
            Assert.Equal(0, MuLungDojo.MobForStage(resting));
            Assert.True(MuLungDojo.IsRestingStage(resting));
        }
    }

    [Fact]
    public void EveryBossHasTheInvincibleCheckerInItsReviveList_ByConvention()
    {
        // MobForStage returns the ids whose wz revive is 9300216 (verified in gamedata.db).
        for (int stage = 1; stage <= MuLungDojo.MaxStage; stage++)
        {
            int mob = MuLungDojo.MobForStage(stage);
            Assert.True(mob == 0 || (mob >= 9300184 && mob <= 9300215));
        }
    }

    [Theory]
    [InlineData(1, 6)]    // band 1 → (1+1)*3
    [InlineData(5, 6)]
    [InlineData(7, 9)]    // band 2 → (2+1)*3
    [InlineData(37, 24)]  // band 7 → (7+1)*3
    [InlineData(6, 0)]    // resting stages award nothing here
    public void PointsForStage_FollowsTheBandTable(int stage, int points)
    {
        Assert.Equal(points, MuLungDojo.PointsForStage(stage));
    }

    [Theory]
    [InlineData(1, 300)]
    [InlineData(5, 300)]
    [InlineData(7, 360)]
    [InlineData(31, 600)]
    [InlineData(38, 900)]
    public void ClockSeconds_IsTheBandMinutesTimesSixty(int stage, int seconds)
    {
        Assert.Equal(seconds, MuLungDojo.ClockSeconds(stage));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 6)]    // 7 - 1
    [InlineData(13, 11)]  // 13 - 2
    [InlineData(38, 32)]  // 38 - 6
    public void DisplayStage_SubtractsTheBandOffset(int stage, int shown)
    {
        Assert.Equal(shown, MuLungDojo.DisplayStage(stage));
    }

    [Fact]
    public void Belts_LineUpWithTheirLevelAndPointRequirements()
    {
        Assert.Equal(5, MuLungDojo.Belts.Length);
        Assert.Equal(MuLungDojo.Belts.Length, MuLungDojo.BeltLevel.Length);
        Assert.Equal(MuLungDojo.Belts.Length, MuLungDojo.BeltPoints.Length);
        Assert.Equal(1132000, MuLungDojo.Belts[0]);
        Assert.Equal(1132004, MuLungDojo.Belts[4]);
        Assert.Equal(new[] { 25, 35, 45, 60, 75 }, MuLungDojo.BeltLevel);
        Assert.Equal(new[] { 200, 1800, 4000, 9200, 17000 }, MuLungDojo.BeltPoints);
    }

    [Fact]
    public void IsDojo_CoversTheWholeInstanceRange_AndTutorial()
    {
        Assert.True(MuLungDojo.IsDojo(925020000));
        Assert.True(MuLungDojo.IsDojo(925033814));
        Assert.True(MuLungDojo.IsTutorialMap(925020010));
        Assert.False(MuLungDojo.IsDojo(925019999));
        Assert.False(MuLungDojo.IsDojo(925040000)); // the back street is its own thing ([DEV])
    }
}
