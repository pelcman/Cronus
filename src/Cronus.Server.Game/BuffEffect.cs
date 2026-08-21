using Cronus.Data;

namespace Cronus.Server.Game;

/// <summary>
/// One active temporary-stat entry in an <c>LP_TemporaryStatSet</c> (ports a <c>TacosBuff</c> entry):
/// the CTS bit it sets, its magnitude, the granting item, and its duration in ms. For an item buff
/// the wire "reason" is the negative item id.
/// </summary>
public readonly record struct BuffStat(int Bit, short Value, int ItemId, int DurationMs);

/// <summary>
/// Maps a consumable's buff spec to the temporary-stat entries and mask used by the buff packets
/// (ports <c>MapleStatEffect</c>'s spec→CTS mapping + <c>OpsSecondaryStat</c> bit layout, JMS v186).
/// All the simple potion stats live in mask word[0]; entries are emitted in ascending bit order.
/// </summary>
public static class BuffEffect
{
    // CTS bit indices for JMS v186 (OpsSecondaryStat.init, JMS >= 186 branch).
    private const int Pad = 0;
    private const int Pdd = 1;
    private const int Mad = 2;
    private const int Mdd = 3;
    private const int Acc = 4;
    private const int Eva = 5;
    private const int Speed = 7;
    private const int Jump = 8;

    /// <summary>The active buff stats a consumable grants, in ascending bit order (empty if none).</summary>
    public static List<BuffStat> FromSpec(ConsumeSpec spec)
    {
        var stats = new List<BuffStat>();
        if (spec.Time <= 0)
        {
            return stats;
        }

        void Add(int value, int bit)
        {
            if (value != 0)
            {
                stats.Add(new BuffStat(bit, (short)value, spec.ItemId, spec.Time));
            }
        }

        Add(spec.Pad, Pad);
        Add(spec.Pdd, Pdd);
        Add(spec.Mad, Mad);
        Add(spec.Mdd, Mdd);
        Add(spec.Acc, Acc);
        Add(spec.Eva, Eva);
        Add(spec.Speed, Speed);
        Add(spec.Jump, Jump);
        return stats;
    }

    /// <summary>The mask word[0] (the only word simple potion stats use) for a set of buff stats.</summary>
    public static uint Word0Mask(IEnumerable<BuffStat> stats)
    {
        uint mask = 0;
        foreach (BuffStat s in stats)
        {
            mask |= 1u << s.Bit;
        }

        return mask;
    }
}
