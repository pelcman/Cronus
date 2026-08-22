using Cronus.Data;
using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>
/// Applies experience gains and level-ups to a character (ports <c>MapleCharacter.levelUp</c> and
/// the HP/MP branches of <c>OnAbilityUpRequestInternal</c>): job-scaled random HP/MP growth, the
/// Improved-Max-HP/MP passive bonuses, the INT/10 MP bonus, AP every level, and SP for jobs.
/// </summary>
public static class CharacterProgression
{
    private const int ApPerLevel = 5;
    private const int SpPerLevel = 3;
    private const int StatMax = 30000;

    // Improving Max HP / MP Increase passives (their x/y feed the growth bonuses).
    private const int ImprovedHpIncrease = 1000001;   // warrior
    private const int ImprovedMpIncrease = 2000001;   // magician
    private const int ImprovedPirateHp = 5100000;     // brawler

    /// <summary>Resolves a learned passive's wz effect, or null (no skill data / not learned).</summary>
    public delegate SkillEffect? EffectResolver(int skillId);

    /// <summary>
    /// Adds <paramref name="amount"/> experience, processing any level-ups. Returns the set of
    /// stats that changed (for an <c>LP_StatChanged</c>). <paramref name="effectOf"/> supplies the
    /// growth passives (Improved Max HP/MP) when available.
    /// </summary>
    public static StatFlag GainExp(Character c, int amount, EffectResolver? effectOf = null)
    {
        StatFlag changed = StatFlag.Exp;
        long exp = (long)c.Exp + Math.Max(0, amount);

        int needed = ExpTable.ExpForLevel(c.Level);
        while (needed > 0 && exp >= needed && c.Level < ExpTable.MaxLevel)
        {
            exp -= needed;
            LevelUp(c, effectOf);
            changed |= StatFlag.Level | StatFlag.MaxHp | StatFlag.Hp
                     | StatFlag.MaxMp | StatFlag.Mp | StatFlag.Ap | StatFlag.Sp;
            needed = ExpTable.ExpForLevel(c.Level);
        }

        // At the cap, retain the last level's worth of exp rather than overflowing.
        c.Exp = (int)Math.Clamp(exp, 0, int.MaxValue);
        return changed;
    }

    /// <summary>
    /// Grants <paramref name="levels"/> full level-ups with real growth (the raise path of the
    /// level command), capped at the level cap. Returns the changed stats.
    /// </summary>
    public static StatFlag ForceLevelUps(Character c, int levels, EffectResolver? effectOf = null)
    {
        StatFlag changed = 0;
        for (int i = 0; i < levels && c.Level < ExpTable.MaxLevel; i++)
        {
            LevelUp(c, effectOf);
            changed |= StatFlag.Level | StatFlag.MaxHp | StatFlag.Hp
                     | StatFlag.MaxMp | StatFlag.Mp | StatFlag.Ap | StatFlag.Sp;
        }

        return changed;
    }

    /// <summary>
    /// Applies the on-death exp penalty (see <see cref="DeathExpLoss"/>; no level-down).
    /// Returns <see cref="StatFlag.Exp"/> if exp changed, or 0.
    /// </summary>
    public static StatFlag ApplyDeathPenalty(Character c, bool inTown = false)
    {
        int loss = DeathExpLoss(c, inTown);
        if (loss <= 0 || c.Exp <= 0)
        {
            return 0;
        }

        c.Exp = Math.Max(0, c.Exp - loss);
        return StatFlag.Exp;
    }

    /// <summary>
    /// The exp a single same-map party member receives from a kill worth <paramref name="baseExp"/>.
    /// A simplified port of MapleStory's party split: the pool is <c>baseExp / (members + 1)</c>, the
    /// killer takes weight 2.0 and every other member 0.3. Solo (a party of one, or no party →
    /// <paramref name="sameMapMemberCount"/> == 1) yields the full <paramref name="baseExp"/> to the
    /// killer, so grouping trades a slice of your exp for giving partners a share. Level/range
    /// modifiers and the class/premium bonuses are not modelled (server is authoritative on exp).
    /// </summary>
    public static int PartyExpShare(int baseExp, int sameMapMemberCount, bool isKiller)
    {
        if (baseExp <= 0 || sameMapMemberCount < 1)
        {
            return 0;
        }

        double fraction = (double)baseExp / (sameMapMemberCount + 1);
        double weight = isKiller ? 2.0 : 0.3;
        return (int)Math.Round(fraction * weight, MidpointRounding.AwayFromZero);
    }

    private const short StatCap = 999;

    /// <summary>
    /// Spends one ability point to raise a stat: STR/DEX/INT/LUK by 1 (capped at 999), or MaxHP/MaxMP
    /// by the job-scaled random amount (ports the CS_MHP/CS_MMP branches of
    /// <c>OnAbilityUpRequestInternal</c>, including the Improved-Max-HP/MP passive bonuses).
    /// Returns the changed stats (the raised stat plus <see cref="StatFlag.Ap"/>), or 0 if it can't
    /// be honored — no AP, a capped stat, or a non-assignable flag.
    /// </summary>
    public static StatFlag SpendAbilityPoint(Character c, StatFlag stat, EffectResolver? effectOf = null)
    {
        if (c.Ap <= 0)
        {
            return 0;
        }

        switch (stat)
        {
            case StatFlag.Str when c.Str < StatCap: c.Str++; break;
            case StatFlag.Dex when c.Dex < StatCap: c.Dex++; break;
            case StatFlag.Int when c.Int < StatCap: c.Int++; break;
            case StatFlag.Luk when c.Luk < StatCap: c.Luk++; break;
            case StatFlag.MaxHp when c.MaxHp < StatMax:
                c.MaxHp = (short)Math.Min(StatMax, c.MaxHp + ApHpGain(c, effectOf));
                break;
            case StatFlag.MaxMp when c.MaxMp < StatMax:
                c.MaxMp = (short)Math.Min(StatMax, c.MaxMp + ApMpGain(c, effectOf));
                break;
            default: return 0; // capped stat, or not an AP-assignable flag
        }

        c.Ap--;
        return stat | StatFlag.Ap;
    }

    /// <summary>HP an AP point buys (job-scaled; ports the CS_MHP table).</summary>
    private static int ApHpGain(Character c, EffectResolver? effectOf) => c.Job switch
    {
        0 => Rand(8, 12),
        >= 100 and <= 132 => Rand(20, 25) + (Learned(c, ImprovedHpIncrease, effectOf)?.X ?? 0),
        >= 200 and <= 232 => Rand(10, 20),
        (>= 300 and <= 322) or (>= 400 and <= 434) => Rand(16, 20),
        >= 500 and <= 522 => Rand(18, 22) + (Learned(c, ImprovedPirateHp, effectOf)?.Y ?? 0),
        _ => Rand(50, 100), // GameMaster / unknown
    };

    /// <summary>MP an AP point buys (job-scaled; ports the CS_MMP table).</summary>
    private static int ApMpGain(Character c, EffectResolver? effectOf) => c.Job switch
    {
        0 => Rand(6, 8),
        >= 100 and <= 132 => Rand(2, 4),
        >= 200 and <= 232 => Rand(18, 20) + (Learned(c, ImprovedMpIncrease, effectOf)?.Y ?? 0) * 2,
        (>= 300 and <= 322) or (>= 400 and <= 434) or (>= 500 and <= 522) => Rand(10, 12),
        _ => Rand(50, 100),
    };

    /// <summary>
    /// Spends <em>all</em> remaining AP across the given base-stat allocations in one shot (the
    /// client's auto-assign; ports <c>OnUserAbilityMassUpRequest</c>). Every allocation must target
    /// STR/DEX/INT/LUK with a non-negative amount, and the amounts must sum to exactly the remaining
    /// AP. Returns the changed stats (each raised stat plus <see cref="StatFlag.Ap"/>), or 0 if the
    /// request is invalid. Only base stats are auto-assignable, matching the reference.
    /// </summary>
    public static StatFlag SpendAllAbilityPoints(Character c, IReadOnlyList<(StatFlag Stat, int Points)> allocations)
    {
        if (allocations.Count == 0)
        {
            return 0;
        }

        long total = 0;
        foreach ((StatFlag stat, int points) in allocations)
        {
            if (points < 0 || points > StatCap
                || stat is not (StatFlag.Str or StatFlag.Dex or StatFlag.Int or StatFlag.Luk))
            {
                return 0;
            }

            total += points;
        }

        if (total == 0 || total != c.Ap)
        {
            return 0; // must spend exactly all the AP the player has
        }

        StatFlag changed = StatFlag.Ap;
        foreach ((StatFlag stat, int points) in allocations)
        {
            switch (stat)
            {
                case StatFlag.Str: c.Str = (short)Math.Min(StatCap, c.Str + points); break;
                case StatFlag.Dex: c.Dex = (short)Math.Min(StatCap, c.Dex + points); break;
                case StatFlag.Int: c.Int = (short)Math.Min(StatCap, c.Int + points); break;
                case StatFlag.Luk: c.Luk = (short)Math.Min(StatCap, c.Luk + points); break;
            }

            changed |= stat;
        }

        c.Ap = 0;
        return changed;
    }

    private static void LevelUp(Character c, EffectResolver? effectOf)
    {
        c.Level++;

        (int hpLo, int hpHi, int mpLo, int mpHi) = c.Job switch
        {
            0 => (12, 16, 10, 12),                                          // Beginner
            >= 100 and <= 132 => (24, 28, 4, 6),                            // Warrior
            >= 200 and <= 232 => (10, 14, 22, 24),                          // Magician
            (>= 300 and <= 322) or (>= 400 and <= 434) => (20, 24, 14, 16), // Bowman / Thief
            >= 500 and <= 522 => (22, 26, 18, 22),                          // Pirate
            _ => (50, 100, 50, 100),                                        // GameMaster / unknown
        };

        int hpGain = Rand(hpLo, hpHi);
        int mpGain = Rand(mpLo, mpHi);

        // Growth passives: Improved Max HP (warrior x / pirate x) and Improved Max MP (mage x*2).
        hpGain += c.Job switch
        {
            >= 100 and <= 132 => Learned(c, ImprovedHpIncrease, effectOf)?.X ?? 0,
            >= 500 and <= 522 => Learned(c, ImprovedPirateHp, effectOf)?.X ?? 0,
            _ => 0,
        };
        if (c.Job is >= 200 and <= 232)
        {
            mpGain += (Learned(c, ImprovedMpIncrease, effectOf)?.X ?? 0) * 2;
        }

        mpGain += c.Int / 10; // the INT bonus (reference uses total INT; base INT here)

        c.MaxHp = (short)Math.Min(StatMax, c.MaxHp + hpGain);
        c.MaxMp = (short)Math.Min(StatMax, c.MaxMp + mpGain);
        c.Hp = c.MaxHp;
        c.Mp = c.MaxMp;
        c.Ap = (short)Math.Min(short.MaxValue, c.Ap + ApPerLevel);
        if (c.Job != 0)
        {
            c.Sp = (short)Math.Min(short.MaxValue, c.Sp + SpPerLevel);
        }
    }

    /// <summary>
    /// The exp lost on death (ports <c>MapleCharacter.playerDead</c>): a share of the level's
    /// requirement — 1% in a town, else <c>0.2/LUK + 0.05</c> (archers use 0.08 for the LUK
    /// part). Beginners lose nothing.
    /// </summary>
    public static int DeathExpLoss(Character c, bool inTown)
    {
        if (c.Job == 0)
        {
            return 0;
        }

        double rate = inTown
            ? 0.01
            : (c.Job / 100 == 3 ? 0.08 : 0.2) / Math.Max((short)1, c.Luk) + 0.05;
        return (int)Math.Min(int.MaxValue, (long)(ExpTable.ExpForLevel(c.Level) * rate));
    }

    /// <summary>The passive's effect at the character's learned level, or null.</summary>
    private static SkillEffect? Learned(Character c, int skillId, EffectResolver? effectOf)
        => c.Skills.ContainsKey(skillId) ? effectOf?.Invoke(skillId) : null;

    private static int Rand(int lo, int hi) => Random.Shared.Next(lo, hi + 1);
}
