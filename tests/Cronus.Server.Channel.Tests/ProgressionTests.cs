using Cronus.Domain;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class ProgressionTests
{
    [Fact]
    public void ExpTable_PreBbValues()
    {
        Assert.Equal(15, ExpTable.ExpForLevel(1));   // level 1 -> 2
        Assert.Equal(34, ExpTable.ExpForLevel(2));
        Assert.Equal(0, ExpTable.ExpForLevel(ExpTable.MaxLevel)); // at cap
    }

    [Fact]
    public void GainExp_BelowThreshold_JustAddsExp()
    {
        var c = new Character { Name = "N", Level = 1, Exp = 0 };
        StatFlag changed = CharacterProgression.GainExp(c, 10);

        Assert.Equal(StatFlag.Exp, changed);
        Assert.Equal(1, c.Level);
        Assert.Equal(10, c.Exp);
    }

    [Fact]
    public void GainExp_LevelsUp_AndCarriesRemainder()
    {
        var c = new Character
        {
            Name = "N", Level = 1, Exp = 0, Job = 100, // a warrior (SP granted)
            MaxHp = 50, MaxMp = 5, Ap = 0, Sp = 0,
        };

        // 15 (1->2) + 34 (2->3) = 49; 50 exp -> level 3 with 1 exp left.
        StatFlag changed = CharacterProgression.GainExp(c, 50);

        Assert.Equal(3, c.Level);
        Assert.Equal(1, c.Exp);
        Assert.True(changed.HasFlag(StatFlag.Level));
        Assert.True(changed.HasFlag(StatFlag.MaxHp));
        Assert.True(changed.HasFlag(StatFlag.Exp));

        Assert.Equal(50 + (2 * 12), c.MaxHp); // +12 HP per level x2
        Assert.Equal(5 + (2 * 10), c.MaxMp);  // +10 MP per level x2
        Assert.Equal(c.MaxHp, c.Hp);          // restored
        Assert.Equal(2 * 5, c.Ap);            // +5 AP per level
        Assert.Equal(2 * 3, c.Sp);            // +3 SP per level (job != 0)
    }

    [Fact]
    public void GainExp_Beginner_GetsNoSp()
    {
        var c = new Character { Name = "N", Level = 1, Exp = 0, Job = 0 };
        CharacterProgression.GainExp(c, 15); // exactly one level

        Assert.Equal(2, c.Level);
        Assert.Equal(0, c.Sp); // beginners get no SP
        Assert.Equal(5, c.Ap);
    }
}
