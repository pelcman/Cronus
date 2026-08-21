using Cronus.Domain;

namespace Cronus.Server.Channel;

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
