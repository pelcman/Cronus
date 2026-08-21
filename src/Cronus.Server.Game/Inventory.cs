using Cronus.Domain;
using Cronus.Server.Login;

namespace Cronus.Server.Game;

/// <summary>An <c>LP_InventoryOperation</c> mode (ports the InvOp modes).</summary>
public enum InvMode : byte
{
    /// <summary>A new item appeared in a slot (carries the full item).</summary>
    Add = 0,

    /// <summary>An existing slot's quantity changed.</summary>
    Update = 1,

    /// <summary>An item moved from one slot to another (equip/unequip/rearrange).</summary>
    Move = 2,

    /// <summary>A slot was emptied.</summary>
    Remove = 3,
}

/// <summary>
/// One inventory change to relay to the client via <c>LP_InventoryOperation</c>. For
/// <see cref="InvMode.Move"/>, <see cref="Position"/> is the source slot and
/// <see cref="DestPosition"/> the destination (either may be negative = an equipped slot).
/// </summary>
public readonly record struct InventoryChange(InvMode Mode, int Tab, short Position, InventoryItem? Item, short Quantity, short DestPosition = 0);

/// <summary>
/// Inventory manipulation on a <see cref="Character"/>'s item list (ports the essentials of
/// <c>MapleInventoryManipulator</c>): stacking add and quantity-removing, returning the changes so
/// the caller can push <c>LP_InventoryOperation</c> and persist. Positive positions are inventory
/// slots; the tab is chosen by the item id's type digit.
/// </summary>
public static class Inventory
{
    /// <summary>Fallback stack limit when an item has no wz <c>slotMax</c>.</summary>
    public const int DefaultSlotMax = 100;

    /// <summary>Inventory tab (1 equip, 2 use, 3 setup, 4 etc, 5 cash) for an item id.</summary>
    public static int Tab(int itemId) => ItemEncoder.ItemType(itemId);

    /// <summary>The item in a tab's slot, or null.</summary>
    public static InventoryItem? ItemAt(Character c, int tab, short position)
        => c.EquippedItems.FirstOrDefault(i => i.Position == position && Tab(i.ItemId) == tab);

    /// <summary>
    /// Adds <paramref name="quantity"/> of an item, stacking onto existing non-full slots first (for
    /// bundle items), then filling new slots up to <paramref name="slotMax"/>. Equips take one slot
    /// each. Returns the changes (adds and updates) to relay and persist.
    /// </summary>
    public static List<InventoryChange> Add(Character c, int itemId, int quantity, int slotMax)
    {
        var changes = new List<InventoryChange>();
        if (quantity <= 0)
        {
            return changes;
        }

        int tab = Tab(itemId);
        int max = Math.Max(1, slotMax);

        if (tab == 1) // equips don't stack — one slot per item
        {
            for (int i = 0; i < quantity; i++)
            {
                short slot = NextFreeSlot(c, tab);
                var item = new InventoryItem { ItemId = itemId, Position = slot, Quantity = 1, CharacterId = c.Id };
                c.EquippedItems.Add(item);
                changes.Add(new InventoryChange(InvMode.Add, tab, slot, item, 1));
            }

            return changes;
        }

        int remaining = quantity;

        // Top up existing stacks of the same item.
        foreach (InventoryItem existing in c.EquippedItems
                     .Where(i => i.Position > 0 && i.ItemId == itemId && i.Quantity < max)
                     .OrderBy(i => i.Position)
                     .ToList())
        {
            if (remaining <= 0)
            {
                break;
            }

            int add = Math.Min(remaining, max - existing.Quantity);
            existing.Quantity += (short)add;
            remaining -= add;
            changes.Add(new InventoryChange(InvMode.Update, tab, existing.Position, existing, existing.Quantity));
        }

        // New stacks for whatever is left.
        while (remaining > 0)
        {
            short slot = NextFreeSlot(c, tab);
            int add = Math.Min(remaining, max);
            var item = new InventoryItem { ItemId = itemId, Position = slot, Quantity = (short)add, CharacterId = c.Id };
            c.EquippedItems.Add(item);
            remaining -= add;
            changes.Add(new InventoryChange(InvMode.Add, tab, slot, item, (short)add));
        }

        return changes;
    }

    /// <summary>
    /// Removes <paramref name="quantity"/> from a specific slot (e.g. consuming a potion). Returns
    /// the change (an update, or a remove when the stack empties), or null if the slot is empty.
    /// </summary>
    public static InventoryChange? RemoveFromSlot(Character c, int tab, short position, int quantity)
    {
        InventoryItem? item = ItemAt(c, tab, position);
        if (item is null || quantity <= 0)
        {
            return null;
        }

        if (item.Quantity <= quantity)
        {
            c.EquippedItems.Remove(item);
            return new InventoryChange(InvMode.Remove, tab, position, null, 0);
        }

        item.Quantity -= (short)quantity;
        return new InventoryChange(InvMode.Update, tab, position, item, item.Quantity);
    }

    /// <summary>
    /// Moves the item at <paramref name="src"/> to <paramref name="dst"/> within a tab (ports
    /// <c>MapleInventoryManipulator.move</c>/<c>equip</c>/<c>unequip</c>): equipping is a positive→
    /// negative move, unequipping negative→positive, and a plain rearrange is positive→positive. If
    /// the destination holds an item they swap slots (a single move op; the client swaps visually).
    /// Returns the change to relay, or null if the source slot is empty. Negative positions are
    /// equipped slots.
    /// </summary>
    public static InventoryChange? Move(Character c, int tab, short src, short dst)
    {
        InventoryItem? item = ItemAt(c, tab, src);
        if (item is null || dst == 0 || src == dst)
        {
            return null;
        }

        InventoryItem? occupant = ItemAt(c, tab, dst);
        item.Position = dst;
        if (occupant is not null)
        {
            occupant.Position = src; // swap the displaced item back into the source slot
        }

        return new InventoryChange(InvMode.Move, tab, src, null, 0, dst);
    }

    /// <summary>
    /// Places an existing item object (e.g. one withdrawn from storage) into the first free slot of
    /// its tab, preserving its stats/quantity, and returns the Add change to relay. Unlike
    /// <see cref="Add"/>, this keeps the same <see cref="InventoryItem"/> instance rather than making
    /// a fresh one, so an equip's stats survive the round-trip.
    /// </summary>
    public static InventoryChange Place(Character c, InventoryItem item)
    {
        int tab = Tab(item.ItemId);
        short slot = NextFreeSlot(c, tab);
        item.Position = slot;
        item.CharacterId = c.Id;
        c.EquippedItems.Add(item);
        return new InventoryChange(InvMode.Add, tab, slot, item, item.Quantity);
    }

    private static short NextFreeSlot(Character c, int tab)
    {
        var used = c.EquippedItems
            .Where(i => i.Position > 0 && Tab(i.ItemId) == tab)
            .Select(i => (int)i.Position)
            .ToHashSet();

        short slot = 1;
        while (used.Contains(slot))
        {
            slot++;
        }

        return slot;
    }
}
