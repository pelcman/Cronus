using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class DamageValidatorTests
{
    private static AttackTarget Target(params int[] damages) =>
        new() { MobObjectId = 2_000_000, Damages = damages };

    [Fact]
    public void ClampLine_WithinCap_PassesThrough()
    {
        Assert.Equal(1234, DamageValidator.ClampLine(1234));
        Assert.Equal(DamageValidator.MaxDamagePerLine, DamageValidator.ClampLine(DamageValidator.MaxDamagePerLine));
    }

    [Fact]
    public void ClampLine_AboveCap_ClampsToCap()
    {
        Assert.Equal(DamageValidator.MaxDamagePerLine, DamageValidator.ClampLine(100_000));
        Assert.Equal(DamageValidator.MaxDamagePerLine, DamageValidator.ClampLine(int.MaxValue));
    }

    [Fact]
    public void Magnitude_StripsCriticalBit()
    {
        // 5000 with the high (critical) bit set is still a 5000 hit.
        int crit = 5000 | unchecked((int)0x80000000);
        Assert.Equal(5000, DamageValidator.Magnitude(crit));
        Assert.Equal(5000, DamageValidator.ClampLine(crit));
    }

    [Fact]
    public void Magnitude_CriticalBitAboveCap_StillClamped()
    {
        // A crit-flagged line whose magnitude exceeds the cap clamps to the cap, not the crit bit.
        int forged = 1_000_000 | unchecked((int)0x80000000);
        Assert.Equal(DamageValidator.MaxDamagePerLine, DamageValidator.ClampLine(forged));
        Assert.True(DamageValidator.IsSuspicious(Target(forged)));
    }

    [Fact]
    public void ValidatedDamage_SumsClampedLines()
    {
        // Two legit lines plus one forged line: forged one contributes only the cap.
        AttackTarget t = Target(3000, 4000, 999_999);
        long expected = 3000L + 4000L + DamageValidator.MaxDamagePerLine;
        Assert.Equal(expected, DamageValidator.ValidatedDamage(t));
    }

    [Fact]
    public void ValidatedDamage_AllLegit_MatchesRawSum()
    {
        AttackTarget t = Target(1000, 2000, 3000);
        Assert.Equal(6000, DamageValidator.ValidatedDamage(t));
        Assert.False(DamageValidator.IsSuspicious(t));
    }

    [Fact]
    public void IsSuspicious_OnlyWhenALineExceedsCap()
    {
        Assert.False(DamageValidator.IsSuspicious(Target(99_999, 50_000)));
        Assert.True(DamageValidator.IsSuspicious(Target(50_000, 100_000)));
    }
}
