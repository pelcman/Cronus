using Cronus.Common;

namespace Cronus.Server.Game;

/// <summary>
/// Server-side handling of client-reported attack damage. MapleStory computes damage on the
/// client (the era's design; the reference server applies it unvalidated), so the server can't
/// recompute it without the full skill / weapon / stat formulas. What it CAN do is bound each
/// line — an optional switch (<see cref="GameConstants.DamageCapEnabled"/>, default off) that
/// clamps every line to <see cref="GameConstants.DamageCap"/> (default 50,000,000). Off, damage
/// passes through exactly as reported (critical bit stripped, negatives floored); on, it becomes
/// "trust but bound" — the server authority the networking design calls for (CLAUDE.md §2).
/// </summary>
public static class DamageValidator
{
    /// <summary>The per-line ceiling in force: the configured cap, or "no limit" when the switch
    /// is off. (Authentic v186 clients render at most 99,999 per line.)</summary>
    public static int MaxDamagePerLine
        => GameConstants.DamageCapEnabled ? GameConstants.DamageCap : int.MaxValue;

    /// <summary>High bit of a damage int is the "critical" flag, not part of the magnitude.</summary>
    private const int CriticalBit = unchecked((int)0x80000000);

    /// <summary>The magnitude of one damage line: critical bit stripped, negatives floored at 0.</summary>
    public static int Magnitude(int rawDamage)
    {
        int dmg = rawDamage & ~CriticalBit; // strip crit flag (keeps low 31 bits)
        return dmg < 0 ? 0 : dmg;
    }

    /// <summary>Clamps one damage line to [0, <see cref="MaxDamagePerLine"/>] — a no-op beyond
    /// the critical-bit strip while the cap switch is off.</summary>
    public static int ClampLine(int rawDamage)
    {
        int dmg = Magnitude(rawDamage);
        int cap = MaxDamagePerLine;
        return dmg > cap ? cap : dmg;
    }

    /// <summary>
    /// True if any line in <paramref name="target"/> exceeds the cap (a cheat/corruption signal).
    /// Always false while the cap switch is off.
    /// </summary>
    public static bool IsSuspicious(AttackTarget target)
    {
        int cap = MaxDamagePerLine;
        foreach (int line in target.Damages)
        {
            if (Magnitude(line) > cap)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The server-accepted total damage to <paramref name="target"/>: each line's magnitude, each
    /// clamped to the per-line cap, summed. This is what the server applies to the mob — never the
    /// raw client total.
    /// </summary>
    public static long ValidatedDamage(AttackTarget target)
    {
        long total = 0;
        foreach (int line in target.Damages)
        {
            total += ClampLine(line);
        }

        return total;
    }
}
