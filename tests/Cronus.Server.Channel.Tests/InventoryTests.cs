using Cronus.Domain;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class InventoryTests
{
    private static Character Hero() => new() { Id = 1, Name = "Hero" };

    [Fact]
    public void Add_NewBundleItem_GoesToFirstFreeSlot()
    {
        var c = Hero();
        var changes = Inventory.Add(c, 2000000, 5, slotMax: 100);

        InventoryChange ch = Assert.Single(changes);
        Assert.Equal(InvMode.Add, ch.Mode);
        Assert.Equal(2, ch.Tab);           // USE
        Assert.Equal(1, ch.Position);      // first slot
        Assert.Equal(5, ch.Quantity);
        Assert.Equal(5, Inventory.ItemAt(c, 2, 1)!.Quantity);
    }

    [Fact]
    public void Add_ExistingStack_StacksUpToSlotMax_ThenSpillsToNewSlot()
    {
        var c = Hero();
        Inventory.Add(c, 2000000, 90, slotMax: 100);      // slot 1 = 90
        var changes = Inventory.Add(c, 2000000, 30, slotMax: 100); // +30 -> 10 tops slot 1, 20 to slot 2

        Assert.Equal(2, changes.Count);
        Assert.Equal(InvMode.Update, changes[0].Mode);
        Assert.Equal(1, changes[0].Position);
        Assert.Equal(100, changes[0].Quantity);           // slot 1 filled to max
        Assert.Equal(InvMode.Add, changes[1].Mode);
        Assert.Equal(2, changes[1].Position);
        Assert.Equal(20, changes[1].Quantity);            // overflow to slot 2
    }

    [Fact]
    public void Add_Equips_DoNotStack_OneSlotEach()
    {
        var c = Hero();
        var changes = Inventory.Add(c, 1302000, 3, slotMax: 100); // a sword (equip)

        Assert.Equal(3, changes.Count);
        Assert.All(changes, ch => Assert.Equal(InvMode.Add, ch.Mode));
        Assert.All(changes, ch => Assert.Equal(1, ch.Tab));       // EQUIP tab
        Assert.All(changes, ch => Assert.Equal(1, ch.Quantity));
        Assert.Equal(new short[] { 1, 2, 3 }, changes.Select(ch => ch.Position).OrderBy(p => p));
    }

    [Fact]
    public void RemoveFromSlot_Decrements_ThenRemovesWhenEmpty()
    {
        var c = Hero();
        Inventory.Add(c, 2000000, 2, slotMax: 100);

        InventoryChange? first = Inventory.RemoveFromSlot(c, 2, 1, 1);
        Assert.Equal(InvMode.Update, first!.Value.Mode);
        Assert.Equal(1, first.Value.Quantity);
        Assert.Equal(1, Inventory.ItemAt(c, 2, 1)!.Quantity);

        InventoryChange? second = Inventory.RemoveFromSlot(c, 2, 1, 1);
        Assert.Equal(InvMode.Remove, second!.Value.Mode);
        Assert.Null(Inventory.ItemAt(c, 2, 1));            // slot now empty
    }

    [Fact]
    public void RemoveFromSlot_EmptySlot_ReturnsNull()
        => Assert.Null(Inventory.RemoveFromSlot(Hero(), 2, 5, 1));

    [Fact]
    public void Gather_CompactsGapsPreservingOrder()
    {
        Character c = Hero();
        c.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 3, Quantity = 10 });
        c.EquippedItems.Add(new InventoryItem { ItemId = 2010000, Position = 7, Quantity = 5 });

        List<InventoryChange> changes = Inventory.Gather(c, 2);

        Assert.Equal(2, changes.Count);
        Assert.Equal((3, (short)1), (changes[0].Position, changes[0].DestPosition));
        Assert.Equal((7, (short)2), ((int)changes[1].Position, changes[1].DestPosition));
        Assert.Equal(1, Inventory.ItemAt(c, 2, 1)!.Position);
        Assert.Equal(2010000, Inventory.ItemAt(c, 2, 2)!.ItemId);
    }

    [Fact]
    public void Sort_OrdersOccupiedPrefixByItemId()
    {
        Character c = Hero();
        c.EquippedItems.Add(new InventoryItem { ItemId = 2050000, Position = 1, Quantity = 1 });
        c.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 2, Quantity = 1 });
        c.EquippedItems.Add(new InventoryItem { ItemId = 2010000, Position = 3, Quantity = 1 });

        List<InventoryChange> changes = Inventory.Sort(c, 2);

        Assert.NotEmpty(changes);
        Assert.Equal(2000000, Inventory.ItemAt(c, 2, 1)!.ItemId);
        Assert.Equal(2010000, Inventory.ItemAt(c, 2, 2)!.ItemId);
        Assert.Equal(2050000, Inventory.ItemAt(c, 2, 3)!.ItemId);
        Assert.All(changes, ch => Assert.Equal(InvMode.Move, ch.Mode)); // swap-moves only
    }

    [Fact]
    public void GatherThenSort_LeavesTabOrderedAndDense()
    {
        Character c = Hero();
        c.EquippedItems.Add(new InventoryItem { ItemId = 2050000, Position = 9, Quantity = 1 });
        c.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 4, Quantity = 1 });
        c.EquippedItems.Add(new InventoryItem { ItemId = 2010000, Position = 2, Quantity = 1 });

        Inventory.Gather(c, 2);
        Inventory.Sort(c, 2);

        Assert.Equal(2000000, Inventory.ItemAt(c, 2, 1)!.ItemId);
        Assert.Equal(2010000, Inventory.ItemAt(c, 2, 2)!.ItemId);
        Assert.Equal(2050000, Inventory.ItemAt(c, 2, 3)!.ItemId);
    }
}
