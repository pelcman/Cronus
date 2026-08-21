using Cronus.Common;
using Cronus.Domain;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// Byte-level checks on the inventory section of the entry CharacterData blob. The item body itself
/// is client-verified for equips (they render); these pin the tab wiring for bundle items.
/// </summary>
public class InventoryEncodingTests
{
    private static byte[] Blob(Character c)
    {
        var w = new PacketWriter(encoding: ServerConfig.Jms186.CodePage);
        CharacterDataEncoder.WriteAllData(w, c);
        return w.ToArray();
    }

    private static Character Base() =>
        new() { Id = 1, Name = "Hero", Level = 10, Job = 0 };

    // One USE item (bundle) = slot(1) + type(1) + itemId(4) + hasUniqueId(1) + noExpiration(8)
    // + quantity(2) + owner-str(2 for "") + flag(2) = 21 bytes.
    private const int BundleItemBytes = 21;

    // A pet item = slot(1) + type(1, =3) + itemId(4) + hasUniqueId(1) + noExpiration(8)
    // + name(13) + level(1) + closeness(2) + fullness(1) + dateDead(8) + petAttr(2) + petSkill(2)
    // + remainLife(4) + attribute(2) + summoned(1) + tail(4) = 55 bytes.
    private const int PetItemBytes = 55;

    [Fact]
    public void PetItem_EncodesAsType3WithPetBody()
    {
        Character empty = Base();
        Character withPet = Base();
        withPet.EquippedItems.Add(new InventoryItem { ItemId = 5000000, Position = 1, Quantity = 1, PetName = "ペット" });

        byte[] blob = Blob(withPet);
        Assert.Equal(Blob(empty).Length + PetItemBytes, blob.Length);

        // The type byte right after the slot byte must be 3 (pet), not 5 (cash bundle).
        byte[] id = BitConverter.GetBytes(5000000);
        int at = IndexOf(blob, id);
        Assert.True(at >= 2);
        Assert.Equal(3, blob[at - 1]);
    }

    [Fact]
    public void PetItems_NeverStack()
    {
        var c = new Character { Id = 1, Name = "Hero" };
        Cronus.Server.Game.Inventory.Add(c, 5000000, 2, slotMax: 100);

        Assert.Equal(2, c.EquippedItems.Count);
        Assert.All(c.EquippedItems, i => Assert.Equal(1, i.Quantity));
        Assert.All(c.EquippedItems, i => Assert.Equal("ペット", i.PetName));
    }

    [Fact]
    public void AddingUseItem_ExtendsBlobByOneItem()
    {
        Character empty = Base();
        Character withItem = Base();
        withItem.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 1, Quantity = 3 });

        Assert.Equal(Blob(empty).Length + BundleItemBytes, Blob(withItem).Length);
    }

    [Fact]
    public void UseItem_ItemIdAppearsInBlob()
    {
        Character c = Base();
        c.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 1, Quantity = 3 });

        byte[] blob = Blob(c);
        // The item id is written little-endian; it should appear in the blob.
        byte[] id = BitConverter.GetBytes(2000000);
        Assert.True(IndexOf(blob, id) >= 0);
    }

    [Fact]
    public void ItemsRouteToTheirTab_ByIdType()
    {
        // A USE, an ETC, and an un-equipped EQUIP item — three different tabs, three item runs.
        Character c = Base();
        c.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 1, Quantity = 1 }); // USE
        c.EquippedItems.Add(new InventoryItem { ItemId = 4000000, Position = 1, Quantity = 1 }); // ETC
        Character justUse = Base();
        justUse.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 1, Quantity = 1 });

        // Adding the ETC item on top of the USE item grows the blob by exactly one more bundle item.
        Assert.Equal(Blob(justUse).Length + BundleItemBytes, Blob(c).Length);
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i + needle.Length <= haystack.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j]) { match = false; break; }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }
}
