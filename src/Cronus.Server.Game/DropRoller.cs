using Cronus.Data;
using Cronus.Server.Login;

namespace Cronus.Server.Game;

/// <summary>
/// The mob drop-roll arithmetic, factored out for testing (ports the per-entry logic of
/// <c>TacosReward.dropFromDatabase</c>). Randomness is injected so the decisions are deterministic
/// under test; the live handler passes <see cref="System.Random.Shared"/>.
/// </summary>
public static class DropRoller
{
    /// <summary>
    /// The x/1000 chance a drop entry rolls. Bosses drop unconditionally (<paramref name="forced"/>);
    /// otherwise the entry drops when <c>roll (0..999) &lt; effective chance × rate</c>. Equip drops
    /// get the reference's ×10 chance boost (<c>retrieveDrop</c> multiplies EQUIP chance by 10).
    /// </summary>
    public static bool ShouldDrop(DropEntry entry, int roll1000, bool forced = false, double rate = 1.0)
        => forced || roll1000 < (int)(EffectiveChance(entry) * rate);

    /// <summary>The entry's chance after the EQUIP ×10 boost the reference applies at load time.</summary>
    public static int EffectiveChance(DropEntry entry)
        => entry.ItemId != 0 && ItemEncoder.ItemType(entry.ItemId) == 1 ? entry.Chance * 10 : entry.Chance;

    /// <summary>
    /// The meso amount for a meso entry: <c>rand(max-min)+min</c> when <c>max &gt; min</c>, else
    /// <c>min</c>. <paramref name="nextInt"/> returns a value in <c>[0, n)</c>.
    /// </summary>
    public static int MesoAmount(DropEntry entry, Func<int, int> nextInt)
        => entry.MaxQuantity > entry.MinQuantity
            ? nextInt(entry.MaxQuantity - entry.MinQuantity) + entry.MinQuantity
            : entry.MinQuantity;

    /// <summary>
    /// The stack count for an item entry (ports <c>de.Maximum != 1 ? rand(range&lt;=0?1:range)+min :
    /// 1</c>, <c>range = |max-min|</c>). Always at least 1.
    /// </summary>
    public static int ItemQuantity(DropEntry entry, Func<int, int> nextInt)
    {
        if (entry.MaxQuantity == 1)
        {
            return 1;
        }

        int range = Math.Abs(entry.MaxQuantity - entry.MinQuantity);
        return Math.Max(1, nextInt(range <= 0 ? 1 : range) + entry.MinQuantity);
    }

    /// <summary>
    /// Horizontal scatter for the <paramref name="droppedCount"/>-th drop off the mob's x (ports
    /// <c>TacosReward.getDropPosition</c>): alternating ±25px steps so a multi-drop kill fans out.
    /// </summary>
    public static int ScatterX(int droppedCount)
        => droppedCount % 2 == 0 ? 25 * (droppedCount + 1) / 2 : -(25 * (droppedCount / 2));
}
