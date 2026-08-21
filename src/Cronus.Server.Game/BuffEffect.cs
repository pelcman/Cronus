using Cronus.Data;

namespace Cronus.Server.Game;

/// <summary>
/// One active temporary-stat entry in an <c>LP_TemporaryStatSet</c> (ports a <c>TacosBuff</c> entry):
/// the CTS bit it sets, its magnitude, the wire "reason", and its duration in ms. The reason is the
/// positive skill id for a skill buff, or the negative item id for an item buff.
/// </summary>
public readonly record struct BuffStat(int Bit, short Value, int Reason, int DurationMs);

/// <summary>
/// Maps a consumable's buff spec to the temporary-stat entries and mask used by the buff packets
/// (ports <c>MapleStatEffect</c>'s spec→CTS mapping + <c>OpsSecondaryStat</c> bit layout, JMS v186).
/// All the simple potion stats live in mask word[0]; entries are emitted in ascending bit order.
/// </summary>
public static class BuffEffect
{
    // Generic CTS bit indices for JMS v186 (OpsSecondaryStat.init, JMS >= 186 branch), shared by
    // item and skill buffs.
    internal const int Pad = 0;
    internal const int Pdd = 1;
    internal const int Mad = 2;
    internal const int Mdd = 3;
    internal const int Acc = 4;
    internal const int Eva = 5;
    internal const int Speed = 7;
    internal const int Jump = 8;

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
                stats.Add(new BuffStat(bit, (short)value, -spec.ItemId, spec.Time)); // item reason = -itemId
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

/// <summary>
/// Maps a skill's effect to the temporary-stat entries for a self-buff cast (ports the skill branch
/// of <c>MapleStatEffect</c>'s spec→CTS mapping, JMS v186). The generic stats reuse the item-buff
/// bits; a handful of signature buffs (Magic Guard, Dark Sight, Booster, Power Guard, Hyper Body) use
/// the skill's <c>x</c>/<c>y</c> for their own CTS bits. The wire reason is the positive skill id.
/// </summary>
public static class SkillBuff
{
    // Skill-only CTS bit indices (OpsSecondaryStat, JMS v186).
    private const int MagicGuard = 9;
    private const int DarkSight = 10;
    private const int Booster = 11;
    private const int PowerGuard = 12;
    private const int MaxHp = 13;
    private const int MaxMp = 14;

    /// <summary>The active buff stats a self-buff skill grants, in ascending bit order (empty if none).</summary>
    public static List<BuffStat> FromEffect(int skillId, SkillEffect effect)
    {
        var stats = new List<BuffStat>();
        if (effect.DurationMs <= 0)
        {
            return stats;
        }

        void Add(int value, int bit)
        {
            if (value != 0)
            {
                stats.Add(new BuffStat(bit, (short)value, skillId, effect.DurationMs)); // skill reason = +skillId
            }
        }

        // Generic stats (same bits as item buffs).
        Add(effect.Pad, BuffEffect.Pad);
        Add(effect.Pdd, BuffEffect.Pdd);
        Add(effect.Mad, BuffEffect.Mad);
        Add(effect.Mdd, BuffEffect.Mdd);
        Add(effect.Acc, BuffEffect.Acc);
        Add(effect.Eva, BuffEffect.Eva);
        Add(effect.Speed, BuffEffect.Speed);
        Add(effect.Jump, BuffEffect.Jump);

        // Signature buffs whose value comes from the skill's x (and y).
        switch (skillId)
        {
            case 2001002: // Magician: Magic Guard
                Add(effect.X, MagicGuard);
                break;
            case 4001003: // Rogue: Dark Sight
                Add(effect.X, DarkSight);
                break;
            case 1101007: // Fighter: Power Guard
            case 1201007: // Page: Power Guard
                Add(effect.X, PowerGuard);
                break;
            case 1301006: // Spearman: Hyper Body
                Add(effect.X, MaxHp);
                Add(effect.Y, MaxMp);
                break;
            default:
                if (IsBooster(skillId))
                {
                    Add(effect.X, Booster);
                }

                break;
        }

        // Entries must go out in ascending bit order.
        stats.Sort((a, b) => a.Bit.CompareTo(b.Bit));
        return stats;
    }

    /// <summary>Weapon/magic booster skills (attack-speed buff via CTS_Booster).</summary>
    private static bool IsBooster(int skillId) => skillId is
        1101004 or 1201004 or 1301004 or // warrior boosters (sword/BW/spear)
        2101004 or 2201004 or            // mage boosters (FP/IL)
        3101004 or 3201004 or            // archer boosters (bow/crossbow)
        4101003 or 4201003;              // thief boosters (claw/dagger)
}
