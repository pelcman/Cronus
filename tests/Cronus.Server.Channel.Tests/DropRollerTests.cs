using Cronus.Data;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class DropRollerTests
{
    [Fact]
    public void ShouldDrop_RollBelowChance_Drops()
    {
        var entry = new DropEntry(2000000, 1, 1, 0, 100); // 10% (100/1000)
        Assert.True(DropRoller.ShouldDrop(entry, roll1000: 99));
        Assert.False(DropRoller.ShouldDrop(entry, roll1000: 100)); // strict less-than
        Assert.False(DropRoller.ShouldDrop(entry, roll1000: 999));
    }

    [Fact]
    public void ShouldDrop_Boss_AlwaysDropsRegardlessOfRoll()
    {
        var entry = new DropEntry(2000000, 1, 1, 0, 1); // 0.1%
        Assert.True(DropRoller.ShouldDrop(entry, roll1000: 999, forced: true));
    }

    [Fact]
    public void EffectiveChance_EquipGetsTenfoldBoost()
    {
        var equip = new DropEntry(1302000, 1, 1, 0, 5); // a one-handed sword (type 1)
        var potion = new DropEntry(2000000, 1, 1, 0, 5); // a use item (type 2)

        Assert.Equal(50, DropRoller.EffectiveChance(equip));
        Assert.Equal(5, DropRoller.EffectiveChance(potion));
    }

    [Fact]
    public void MesoAmount_RangedAndFixed()
    {
        // max > min: rand(max-min) + min. With nextInt stubbed to its argument-1 (top of the range):
        var ranged = new DropEntry(0, 8, 12, 0, 800);
        Assert.Equal(11, DropRoller.MesoAmount(ranged, n => n - 1)); // (12-8-1) + 8 = 11
        Assert.Equal(8, DropRoller.MesoAmount(ranged, _ => 0));      // bottom of the range

        var fixedAmt = new DropEntry(0, 50, 50, 0, 800);
        Assert.Equal(50, DropRoller.MesoAmount(fixedAmt, _ => 999)); // max == min -> min, no roll
    }

    [Fact]
    public void ItemQuantity_MaxOne_IsAlwaysOne()
    {
        var single = new DropEntry(2000000, 1, 1, 0, 100);
        Assert.Equal(1, DropRoller.ItemQuantity(single, _ => 999));
    }

    [Fact]
    public void ItemQuantity_RangedStack()
    {
        var stack = new DropEntry(2000000, 1, 3, 0, 100); // range |3-1| = 2
        Assert.Equal(1, DropRoller.ItemQuantity(stack, _ => 0)); // 0 + 1
        Assert.Equal(2, DropRoller.ItemQuantity(stack, _ => 1)); // 1 + 1
    }

    [Fact]
    public void ScatterX_FansOutAlternating()
    {
        // Mirrors TacosReward.getDropPosition's integer arithmetic (25px steps, alternating sides).
        Assert.Equal(12, DropRoller.ScatterX(0));
        Assert.Equal(0, DropRoller.ScatterX(1));
        Assert.Equal(37, DropRoller.ScatterX(2));
        Assert.Equal(-25, DropRoller.ScatterX(3));
    }
}
