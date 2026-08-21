namespace Cronus.Server.Game;

/// <summary>
/// Server-authoritative sanity checks on client-reported attack damage. MapleStory sends damage
/// from the client (the norm for the era), so the server can't recompute it without full skill /
/// weapon / stat formulas — but it can reject values that no legitimate pre-Big-Bang client could
/// produce. The concrete invariant used here is the hard per-line damage cap: pre-Big-Bang JMS
/// (v186, before the Big Bang raised it) clamps every damage line to 99,999 client-side, so any
/// line above that is impossible from a real client and is clamped down. This turns
/// <c>ApplyAttackDamageAsync</c> from "trust the client" into "trust but bound" — the server
/// authority the networking design calls for (see CLAUDE.md §2), without needing the full formula.
/// </summary>
public static class DamageValidator
{
    /// <summary>
    /// Pre-Big-Bang JMS damage cap per hit line. A real v186 client cannot render or send a line
    /// above this, so anything larger is a corrupted or forged value.
    /// </summary>
    public const int MaxDamagePerLine = 99_999;

    /// <summary>High bit of a damage int is the "critical" flag, not part of the magnitude.</summary>
    private const int CriticalBit = unchecked((int)0x80000000);

    /// <summary>The magnitude of one damage line: critical bit stripped, negatives floored at 0.</summary>
    public static int Magnitude(int rawDamage)
    {
        int dmg = rawDamage & ~CriticalBit; // strip crit flag (keeps low 31 bits)
        return dmg < 0 ? 0 : dmg;
    }

    /// <summary>Clamps one damage line to the legal range [0, <see cref="MaxDamagePerLine"/>].</summary>
    public static int ClampLine(int rawDamage)
    {
        int dmg = Magnitude(rawDamage);
        return dmg > MaxDamagePerLine ? MaxDamagePerLine : dmg;
    }

    /// <summary>
    /// True if any line in <paramref name="target"/> exceeds the cap — i.e. this attack carries a
    /// value a legitimate client could not have produced (a cheat/corruption signal).
    /// </summary>
    public static bool IsSuspicious(AttackTarget target)
    {
        foreach (int line in target.Damages)
        {
            if (Magnitude(line) > MaxDamagePerLine)
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
