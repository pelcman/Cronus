using Cronus.Data;
using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>The outcome of applying an upgrade scroll (ports <c>IEquip.ScrollResult</c>).</summary>
public enum ScrollResult
{
    /// <summary>The scroll failed; a slot was consumed (unless white-scroll-protected).</summary>
    Fail = 0,

    /// <summary>The scroll worked; stats applied / slots restored.</summary>
    Success = 1,

    /// <summary>The scroll failed and the curse destroyed the equip.</summary>
    Curse = 2,
}

/// <summary>
/// The scroll rules (ports the JMS v186-relevant scope of
/// <c>MapleItemInformationProvider.scrollEquipWithId</c>): normal stat scrolls, clean slates
/// (2049000-8), chaos scrolls (20491xx), and white-scroll protection. Pure state mutation on the
/// equip — the channel handler drives packets and inventory around it.
/// </summary>
public static class Scrolling
{
    public const int WhiteScrollItemId = 2340000;

    /// <summary>Clean slates restore a used slot: 2049000-5 give +1, 2049006-8 give +2.</summary>
    public static bool IsCleanSlate(int scrollId) => scrollId is >= 2049000 and <= 2049008;

    /// <summary>Chaos scrolls (20491xx) randomize existing stats instead of adding fixed ones.</summary>
    public static bool IsChaosScroll(int scrollId) => scrollId / 100 == 20491;

    /// <summary>
    /// A normal scroll targets one equip family: the scroll id's category digits must equal the
    /// equip id's (ports <c>MapleItemInformationProvider.canScroll</c>) — e.g. 2043000 (one-handed
    /// sword ATT) ↔ 1302xxx.
    /// </summary>
    public static bool CanScroll(int scrollId, int equipId)
        => scrollId / 100 % 100 == equipId / 10000 % 100;

    /// <summary>
    /// Applies a scroll to an equip and reports the outcome. The equip is mutated in place; a
    /// <see cref="ScrollResult.Curse"/> means the caller must destroy it. <paramref name="equipTuc"/>
    /// is the equip's wz base slot count (for clean slates); <paramref name="whiteScroll"/> prevents
    /// the slot loss on failure.
    /// </summary>
    public static ScrollResult Apply(InventoryItem equip, int scrollId, ScrollSpec spec, int equipTuc,
        bool whiteScroll, Random rng)
    {
        int success = IsChaosScroll(scrollId) && scrollId == 2049100 ? 100 : spec.Success;

        if (rng.Next(100) <= success)
        {
            if (IsCleanSlate(scrollId))
            {
                int restore = scrollId >= 2049006 ? 2 : 1;
                if (equip.Level + equip.UpgradeSlots < equipTuc)
                {
                    equip.UpgradeSlots = (byte)(equip.UpgradeSlots + restore);
                    return ScrollResult.Success; // success = a slot actually came back
                }

                return ScrollResult.Fail; // nothing to restore (reference reports no change)
            }

            if (IsChaosScroll(scrollId))
            {
                int range = scrollId == 2049116 ? 10 : 5;
                ShakeStats(equip, range, rng);
            }
            else
            {
                AddStats(equip, spec.Stats);
            }

            equip.UpgradeSlots--;
            equip.Level++;
            return ScrollResult.Success;
        }

        // Failure: the attempt still burns a slot unless a white scroll protected it.
        if (!whiteScroll && !IsCleanSlate(scrollId))
        {
            equip.UpgradeSlots--;
        }

        return rng.Next(99) < spec.Cursed ? ScrollResult.Curse : ScrollResult.Fail;
    }

    private static void AddStats(InventoryItem e, EquipStats s)
    {
        e.Str += s.Str;
        e.Dex += s.Dex;
        e.Int += s.Int;
        e.Luk += s.Luk;
        e.Hp += s.Hp;
        e.Mp += s.Mp;
        e.Watk += s.Watk;
        e.Matk += s.Matk;
        e.Wdef += s.Wdef;
        e.Mdef += s.Mdef;
        e.Acc += s.Acc;
        e.Avoid += s.Avoid;
        e.Hands += s.Hands;
        e.Speed += s.Speed;
        e.Jump += s.Jump;
    }

    /// <summary>Chaos: every nonzero stat drifts by ±rand(range) (ports the chaos branch).</summary>
    private static void ShakeStats(InventoryItem e, int range, Random rng)
    {
        short Shake(short value) => value > 0
            ? (short)Math.Max(0, value + rng.Next(range) * (rng.Next(2) == 0 ? 1 : -1))
            : value;

        e.Str = Shake(e.Str);
        e.Dex = Shake(e.Dex);
        e.Int = Shake(e.Int);
        e.Luk = Shake(e.Luk);
        e.Hp = Shake(e.Hp);
        e.Mp = Shake(e.Mp);
        e.Watk = Shake(e.Watk);
        e.Matk = Shake(e.Matk);
        e.Wdef = Shake(e.Wdef);
        e.Mdef = Shake(e.Mdef);
        e.Acc = Shake(e.Acc);
        e.Avoid = Shake(e.Avoid);
        e.Speed = Shake(e.Speed);
        e.Jump = Shake(e.Jump);
    }
}
