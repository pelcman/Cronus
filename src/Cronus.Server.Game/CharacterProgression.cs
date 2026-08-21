using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>
/// Applies experience gains and level-ups to a character. HP/MP gains use simple flat per-level
/// values (the server is authoritative on HP/MP, so exact wz formulas are not required); AP is
/// granted every level and SP to non-beginner jobs.
/// </summary>
public static class CharacterProgression
{
    private const int HpPerLevel = 12;
    private const int MpPerLevel = 10;
    private const int ApPerLevel = 5;
    private const int SpPerLevel = 3;

    /// <summary>
    /// Adds <paramref name="amount"/> experience, processing any level-ups. Returns the set of
    /// stats that changed (for an <c>LP_StatChanged</c>).
    /// </summary>
    public static StatFlag GainExp(Character c, int amount)
    {
        StatFlag changed = StatFlag.Exp;
        long exp = (long)c.Exp + Math.Max(0, amount);

        int needed = ExpTable.ExpForLevel(c.Level);
        while (needed > 0 && exp >= needed && c.Level < ExpTable.MaxLevel)
        {
            exp -= needed;
            LevelUp(c);
            changed |= StatFlag.Level | StatFlag.MaxHp | StatFlag.Hp
                     | StatFlag.MaxMp | StatFlag.Mp | StatFlag.Ap | StatFlag.Sp;
            needed = ExpTable.ExpForLevel(c.Level);
        }

        // At the cap, retain the last level's worth of exp rather than overflowing.
        c.Exp = (int)Math.Clamp(exp, 0, int.MaxValue);
        return changed;
    }

    /// <summary>
    /// Applies the on-death exp penalty: loses a tenth of the accumulated (current-level) exp, no
    /// level-down. Returns <see cref="StatFlag.Exp"/> if it changed, or 0 when there was none to
    /// lose. Simplified — MapleStory scales the loss by level and exempts towns.
    /// </summary>
    public static StatFlag ApplyDeathPenalty(Character c)
    {
        if (c.Exp <= 0)
        {
            return 0;
        }

        c.Exp -= c.Exp / 10;
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
    private const int MaxHpPerAp = 15; // simplified flat gain (reference is job-scaled random)
    private const int MaxMpPerAp = 12;

    /// <summary>
    /// Spends one ability point to raise a stat: STR/DEX/INT/LUK by 1 (capped at 999), or MaxHP/MaxMP
    /// by a flat amount. Returns the changed stats (the raised stat plus <see cref="StatFlag.Ap"/>),
    /// or 0 if it can't be honored — no AP, a capped base stat, or a non-assignable flag. Ports
    /// <c>OnAbilityUpRequest</c>; HP/MP gains are simplified flat values (server owns HP/MP).
    /// </summary>
    public static StatFlag SpendAbilityPoint(Character c, StatFlag stat)
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
            case StatFlag.MaxHp: c.MaxHp = (short)Math.Min(short.MaxValue, c.MaxHp + MaxHpPerAp); break;
            case StatFlag.MaxMp: c.MaxMp = (short)Math.Min(short.MaxValue, c.MaxMp + MaxMpPerAp); break;
            default: return 0; // capped base stat, or not an AP-assignable flag
        }

        c.Ap--;
        return stat | StatFlag.Ap;
    }

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

    private static void LevelUp(Character c)
    {
        c.Level++;
        c.MaxHp = (short)Math.Min(short.MaxValue, c.MaxHp + HpPerLevel);
        c.MaxMp = (short)Math.Min(short.MaxValue, c.MaxMp + MpPerLevel);
        c.Hp = c.MaxHp;
        c.Mp = c.MaxMp;
        c.Ap = (short)Math.Min(short.MaxValue, c.Ap + ApPerLevel);
        if (c.Job != 0)
        {
            c.Sp = (short)Math.Min(short.MaxValue, c.Sp + SpPerLevel);
        }
    }
}
