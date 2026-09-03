using Cronus.Common;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// The optional per-line damage cap. Default: OFF (client damage passes through, like the
/// reference); when enabled, every line is clamped to <see cref="GameConstants.DamageCap"/>.
/// Each test restores the static config it touched.
/// </summary>
public class DamageValidatorTests
{
    private const int CriticalBit = unchecked((int)0x80000000);

    private static AttackTarget Target(params int[] damages) =>
        new() { MobObjectId = 2_000_000, Damages = damages };

    private static void WithCap(bool enabled, int cap, Action body)
    {
        bool oldEnabled = GameConstants.DamageCapEnabled;
        int oldCap = GameConstants.DamageCap;
        try
        {
            GameConstants.DamageCapEnabled = enabled;
            GameConstants.DamageCap = cap;
            body();
        }
        finally
        {
            GameConstants.DamageCapEnabled = oldEnabled;
            GameConstants.DamageCap = oldCap;
        }
    }

    [Fact]
    public void Defaults_CapIsOff_At50Million()
    {
        Assert.False(GameConstants.DamageCapEnabled);
        Assert.Equal(50_000_000, GameConstants.DamageCap);
    }

    [Fact]
    public void CapOff_PassesAnyMagnitudeThrough()
    {
        WithCap(enabled: false, cap: 50_000_000, () =>
        {
            Assert.Equal(1234, DamageValidator.ClampLine(1234));
            Assert.Equal(100_000, DamageValidator.ClampLine(100_000));
            Assert.Equal(999_999_999, DamageValidator.ClampLine(999_999_999));
            Assert.False(DamageValidator.IsSuspicious(Target(999_999_999)));
            Assert.Equal(3000L + 4000L + 999_999_999L, DamageValidator.ValidatedDamage(Target(3000, 4000, 999_999_999)));
        });
    }

    [Fact]
    public void CapOn_ClampsEachLineToTheConfiguredCeiling()
    {
        WithCap(enabled: true, cap: 50_000_000, () =>
        {
            Assert.Equal(50_000_000, DamageValidator.MaxDamagePerLine);
            Assert.Equal(1234, DamageValidator.ClampLine(1234));
            Assert.Equal(50_000_000, DamageValidator.ClampLine(50_000_000));
            Assert.Equal(50_000_000, DamageValidator.ClampLine(50_000_001));
            Assert.Equal(50_000_000, DamageValidator.ClampLine(int.MaxValue));
            Assert.True(DamageValidator.IsSuspicious(Target(50_000_001)));
            Assert.False(DamageValidator.IsSuspicious(Target(50_000_000)));
            Assert.Equal(3000L + 4000L + 50_000_000L, DamageValidator.ValidatedDamage(Target(3000, 4000, 60_000_000)));
        });
    }

    [Fact]
    public void CapOn_HonoursACustomCeiling()
    {
        WithCap(enabled: true, cap: 99_999, () =>
        {
            Assert.Equal(99_999, DamageValidator.ClampLine(100_000)); // the authentic v186 client ceiling
        });
    }

    [Fact]
    public void Magnitude_StripsTheCriticalBit_RegardlessOfCap()
    {
        // Bit 31 is the client's critical flag, not magnitude: a "negative" int is simply a
        // critical line whose low 31 bits are the damage. Nothing is floored to 0.
        int crit = 5000 | CriticalBit;
        Assert.Equal(5000, DamageValidator.Magnitude(crit));
        Assert.Equal(5000, DamageValidator.ClampLine(crit));
        Assert.Equal(-42 & 0x7FFFFFFF, DamageValidator.Magnitude(-42));
        Assert.Equal(6000, DamageValidator.ValidatedDamage(Target(1000, 2000, 3000)));
    }

    [Fact]
    public void CapOn_BoundsAForgedCriticalLine_CapOffDoesNot()
    {
        int forged = int.MaxValue | CriticalBit; // magnitude 0x7FFFFFFF
        WithCap(enabled: true, cap: 50_000_000, () => Assert.Equal(50_000_000, DamageValidator.ClampLine(forged)));
        WithCap(enabled: false, cap: 50_000_000, () => Assert.Equal(int.MaxValue, DamageValidator.ClampLine(forged)));
    }
}
