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

        Assert.InRange(c.MaxHp, 50 + 2 * 24, 50 + 2 * 28); // warrior: +24..28 HP per level
        Assert.InRange(c.MaxMp, 5 + 2 * 4, 5 + 2 * 6);     // warrior: +4..6 MP per level
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

    [Fact]
    public void ApplyDeathPenalty_LosesATenthOfExp()
    {
        var c = new Character { Name = "N", Level = 5, Exp = 1000 };

        StatFlag changed = CharacterProgression.ApplyDeathPenalty(c);

        Assert.Equal(StatFlag.Exp, changed);
        Assert.Equal(900, c.Exp);  // -10%
        Assert.Equal(5, c.Level);  // no level-down
    }

    [Fact]
    public void ApplyDeathPenalty_WithNoExp_ChangesNothing()
    {
        var c = new Character { Name = "N", Level = 1, Exp = 0 };

        StatFlag changed = CharacterProgression.ApplyDeathPenalty(c);

        Assert.Equal((StatFlag)0, changed);
        Assert.Equal(0, c.Exp);
    }

    [Fact]
    public void PartyExpShare_SoloGetsFullExp()
    {
        // A party of one (or the no-party path) still yields the full base exp to the killer.
        Assert.Equal(1000, CharacterProgression.PartyExpShare(1000, sameMapMemberCount: 1, isKiller: true));
    }

    [Fact]
    public void PartyExpShare_KillerGetsMoreThanPartner()
    {
        // Two members: pool = 1000/3 ≈ 333.33; killer x2 ≈ 667, partner x0.3 ≈ 100.
        int killer = CharacterProgression.PartyExpShare(1000, sameMapMemberCount: 2, isKiller: true);
        int partner = CharacterProgression.PartyExpShare(1000, sameMapMemberCount: 2, isKiller: false);

        Assert.Equal(667, killer);
        Assert.Equal(100, partner);
        Assert.True(killer > partner);
    }

    [Fact]
    public void PartyExpShare_ZeroOrInvalid_GivesNothing()
    {
        Assert.Equal(0, CharacterProgression.PartyExpShare(0, 2, isKiller: true));
        Assert.Equal(0, CharacterProgression.PartyExpShare(1000, 0, isKiller: true));
    }

    [Fact]
    public void SpendAbilityPoint_RaisesStat_AndSpendsAp()
    {
        var c = new Character { Name = "N", Str = 4, Ap = 3 };

        StatFlag changed = CharacterProgression.SpendAbilityPoint(c, StatFlag.Str);

        Assert.Equal(StatFlag.Str | StatFlag.Ap, changed);
        Assert.Equal(5, c.Str);
        Assert.Equal(2, c.Ap);
    }

    [Fact]
    public void SpendAbilityPoint_NoAp_DoesNothing()
    {
        var c = new Character { Name = "N", Dex = 4, Ap = 0 };

        Assert.Equal((StatFlag)0, CharacterProgression.SpendAbilityPoint(c, StatFlag.Dex));
        Assert.Equal(4, c.Dex);
    }

    [Fact]
    public void SpendAbilityPoint_CappedStat_DoesNotSpend()
    {
        var c = new Character { Name = "N", Luk = 999, Ap = 5 };

        Assert.Equal((StatFlag)0, CharacterProgression.SpendAbilityPoint(c, StatFlag.Luk));
        Assert.Equal(999, c.Luk);
        Assert.Equal(5, c.Ap); // AP not spent on a rejected raise
    }

    [Fact]
    public void SpendAbilityPoint_MaxHp_AddsJobScaledAmount()
    {
        var c = new Character { Name = "N", MaxHp = 100, Ap = 1, Job = 100 }; // warrior

        StatFlag changed = CharacterProgression.SpendAbilityPoint(c, StatFlag.MaxHp);

        Assert.Equal(StatFlag.MaxHp | StatFlag.Ap, changed);
        Assert.InRange(c.MaxHp, 120, 125); // warrior AP-into-HP: +20..25
        Assert.Equal(0, c.Ap);
    }

    [Fact]
    public void SpendAbilityPoint_MaxHp_WarriorPassive_AddsItsX()
    {
        var c = new Character { Name = "N", MaxHp = 100, Ap = 1, Job = 100 };
        c.Skills[1000001] = 5; // Improved Max HP Increase learned

        CharacterProgression.SpendAbilityPoint(c, StatFlag.MaxHp,
            id => id == 1000001 ? new Cronus.Data.SkillEffect { X = 10 } : null);

        Assert.InRange(c.MaxHp, 130, 135); // 20..25 + passive x (10)
    }

    [Fact]
    public void SpendAbilityPoint_MaxMp_Magician_UsesItsRange()
    {
        var c = new Character { Name = "N", MaxMp = 50, Ap = 1, Job = 200 };

        CharacterProgression.SpendAbilityPoint(c, StatFlag.MaxMp);

        Assert.InRange(c.MaxMp, 68, 70); // magician AP-into-MP: +18..20
    }

    [Fact]
    public void ForceLevelUps_GrowsLikeRealLevels()
    {
        var c = new Character { Name = "N", Level = 1, Job = 200, MaxHp = 50, MaxMp = 5, Int = 20 };

        StatFlag changed = CharacterProgression.ForceLevelUps(c, 10);

        Assert.Equal(11, c.Level);
        Assert.True(changed.HasFlag(StatFlag.MaxHp));
        Assert.InRange(c.MaxHp, 50 + 10 * 10, 50 + 10 * 14);      // magician: +10..14 HP
        Assert.InRange(c.MaxMp, 5 + 10 * (22 + 2), 5 + 10 * (24 + 2)); // +22..24 MP + Int/10 (2)
        Assert.Equal(50, c.Ap);
        Assert.Equal(30, c.Sp);
    }

    [Fact]
    public void SpendAbilityPoint_NonAssignableFlag_DoesNothing()
    {
        var c = new Character { Name = "N", Ap = 5 };

        Assert.Equal((StatFlag)0, CharacterProgression.SpendAbilityPoint(c, StatFlag.Exp));
        Assert.Equal(5, c.Ap);
    }

    [Fact]
    public void SpendAllAbilityPoints_SpreadsAcrossStats_AndEmptiesAp()
    {
        var c = new Character { Name = "N", Str = 4, Dex = 4, Ap = 5 };

        StatFlag changed = CharacterProgression.SpendAllAbilityPoints(c,
            new[] { (StatFlag.Str, 3), (StatFlag.Dex, 2) });

        Assert.Equal(StatFlag.Str | StatFlag.Dex | StatFlag.Ap, changed);
        Assert.Equal(7, c.Str);
        Assert.Equal(6, c.Dex);
        Assert.Equal(0, c.Ap);
    }

    [Fact]
    public void SpendAllAbilityPoints_TotalMustEqualAp()
    {
        var c = new Character { Name = "N", Str = 4, Ap = 5 };

        // Spending only 3 of 5 AP is rejected (auto-assign spends all).
        Assert.Equal((StatFlag)0, CharacterProgression.SpendAllAbilityPoints(c, new[] { (StatFlag.Str, 3) }));
        Assert.Equal(4, c.Str);
        Assert.Equal(5, c.Ap);
    }

    [Fact]
    public void SpendAllAbilityPoints_RejectsNonBaseStatOrNegative()
    {
        var c = new Character { Name = "N", Ap = 5 };

        Assert.Equal((StatFlag)0, CharacterProgression.SpendAllAbilityPoints(c, new[] { (StatFlag.MaxHp, 5) }));
        Assert.Equal((StatFlag)0, CharacterProgression.SpendAllAbilityPoints(c, new[] { (StatFlag.Str, -1), (StatFlag.Dex, 6) }));
        Assert.Equal(5, c.Ap); // nothing spent
    }
}
