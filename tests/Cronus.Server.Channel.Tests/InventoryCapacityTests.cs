using Cronus.Common;
using Cronus.Domain;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// Inventory capacity: tabs hold <see cref="GameConstants.InventorySlotsPerTab"/> slots — the
/// unbounded free-slot search used to spill items into invisible slots past the client's grid.
/// </summary>
public class InventoryCapacityTests
{
    private static Character FullEtcTab()
    {
        var c = new Character { Id = 1, Name = "Packrat" };
        for (short slot = 1; slot <= GameConstants.InventorySlotsPerTab; slot++)
        {
            c.EquippedItems.Add(new InventoryItem { ItemId = 4000000 + slot, Position = slot, Quantity = 1 });
        }

        return c;
    }

    [Fact]
    public void AddToAFullTab_GrantsNothing_InsteadOfAnInvisibleSlot()
    {
        Character c = FullEtcTab();

        List<InventoryChange> changes = Inventory.Add(c, 4009999, 1, slotMax: 100);

        Assert.Empty(changes);
        Assert.DoesNotContain(c.EquippedItems, i => i.ItemId == 4009999);
        Assert.All(c.EquippedItems, i => Assert.InRange((int)i.Position, 1, GameConstants.InventorySlotsPerTab));
    }

    [Fact]
    public void AddToAFullTab_StillStacksOntoAnExistingPile()
    {
        Character c = FullEtcTab(); // slot 1 holds item 4000001 x1

        List<InventoryChange> changes = Inventory.Add(c, 4000001, 5, slotMax: 100);

        InventoryChange update = Assert.Single(changes);
        Assert.Equal(InvMode.Update, update.Mode);
        Assert.Equal(6, update.Quantity);
    }

    [Fact]
    public void CanAdd_ReportsHeadroomAndFreeSlots()
    {
        Character c = FullEtcTab();

        Assert.False(Inventory.CanAdd(c, 4009999, 1, 100));      // full tab, new item
        Assert.True(Inventory.CanAdd(c, 4000001, 99, 100));      // stacks onto the existing pile
        Assert.False(Inventory.CanAdd(c, 4000001, 100, 100));    // pile can take 99, not 100

        var fresh = new Character { Id = 2, Name = "Empty" };
        Assert.True(Inventory.CanAdd(fresh, 1302000, GameConstants.InventorySlotsPerTab, 1)); // equips fill the tab
        Assert.False(Inventory.CanAdd(fresh, 1302000, GameConstants.InventorySlotsPerTab + 1, 1));
    }
}
