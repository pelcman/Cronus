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
    public static uint Word0Mask(IEnumerable<BuffStat> stats) => (uint)Mask64(stats);

    /// <summary>The CTS mask covering bits 0-63 (word[0] low, word[1] high) for a set of stats.</summary>
    public static ulong Mask64(IEnumerable<BuffStat> stats)
    {
        ulong mask = 0;
        foreach (BuffStat s in stats)
        {
            mask |= 1ul << s.Bit;
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

    /// <summary>CTS_ComboCounter (bit 21) — the Crusader combo orb display.</summary>
    public const int ComboCounter = 21;

    /// <summary>Crusader Combo Attack (the orb buff).</summary>
    public const int ComboAttackSkill = 1111002;

    // Further signature bits (OpsSecondaryStat, JMS v186; 32+ live in mask word[1]).
    private const int Invincible = 15;
    private const int SoulArrow = 16;
    private const int WeaponCharge = 22;
    private const int DragonBlood = 23;
    private const int HolySymbol = 24;
    private const int MesoUp = 25;
    private const int ShadowPartner = 26;
    private const int PickPocket = 27;
    private const int MesoGuard = 28;
    private const int BasicStatUp = 35; // Maple Warrior
    private const int Stance = 36;
    private const int SharpEyes = 37;
    private const int ManaReflection = 38;

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
            case ComboAttackSkill: // Crusader: Combo Attack (value = orbs + 1, fresh = 1)
                Add(1, ComboCounter);
                break;
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
            case 2301003: // Cleric: Invincible
                Add(effect.X, Invincible);
                break;
            case 3101004: // Hunter / Crossbowman: Soul Arrow
            case 3201004:
                Add(effect.X, SoulArrow);
                break;
            case 1211003: // White Knight charges (sword/BW fire/ice/thunder)
            case 1211004:
            case 1211005:
            case 1211006:
            case 1211007:
            case 1211008:
                Add(effect.X, WeaponCharge);
                break;
            case 1311008: // Dragon Knight: Dragon Blood
                Add(effect.X, DragonBlood);
                break;
            case 2311003: // Priest: Holy Symbol
                Add(effect.X, HolySymbol);
                break;
            case 4111001: // Hermit: Meso Up
                Add(effect.X, MesoUp);
                break;
            case 4111002: // Hermit: Shadow Partner
                Add(effect.X, ShadowPartner);
                break;
            case 4211003: // Chief Bandit: Pick Pocket
                Add(effect.X, PickPocket);
                break;
            case 4211005: // Chief Bandit: Meso Guard
                Add(effect.X, MesoGuard);
                break;
            case 1121002: // Hero / Paladin / Dark Knight: Stance
            case 1221002:
            case 1321002:
                Add(effect.X, Stance);
                break;
            case 3121002: // Bowmaster / Marksman: Sharp Eyes (value = x<<8 | y)
            case 3221002:
                Add((effect.X << 8) | effect.Y, SharpEyes);
                break;
            case 2121002: // Arch Mage / Bishop: Mana Reflection
            case 2221002:
            case 2321002:
                Add(1, ManaReflection);
                break;
            case 1121000: // Maple Warrior (every 4th job)
            case 1221000:
            case 1321000:
            case 2121000:
            case 2221000:
            case 2321000:
            case 3121000:
            case 3221000:
            case 4121000:
            case 4221000:
            case 5121000:
            case 5221000:
                Add(effect.X, BasicStatUp);
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
        4101003 or 4201002;              // thief boosters (claw 4101003 / dagger 4201002 — 4201003 is Haste)
}
