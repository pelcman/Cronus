using Cronus.Common;
using Cronus.Domain;
using Cronus.Network.Packets;
using Xunit;

namespace Cronus.Server.Login.Tests;

/// <summary>
/// AvatarLook with cash overlays (ports <c>DataAvatarLook.Encode</c>): the -1xx cash item takes
/// the visible slot, the covered base equip moves to the masked list, and the cash weapon
/// (-111) rides as the weapon-sticker int — the layer that made worn cash outfits vanish.
/// </summary>
public class AvatarLookTests
{
    private static (Dictionary<byte, int> Visible, List<(byte Slot, int ItemId)> Masked, int Sticker) Decode(Character c)
    {
        var w = new PacketWriter(encoding: ServerConfig.Jms186.CodePage);
        CharacterEncoder.WriteAvatarLook(w, c);
        var r = new PacketReader(w.ToArray(), ServerConfig.Jms186.CodePage);

        r.ReadByte(); r.ReadByte(); r.ReadInt(); r.ReadByte(); r.ReadInt(); // gender/skin/face/pad/hair

        var visible = new Dictionary<byte, int>();
        for (byte slot = r.ReadByte(); slot != 0xFF; slot = r.ReadByte())
        {
            visible[slot] = r.ReadInt();
        }

        var masked = new List<(byte, int)>();
        for (byte slot = r.ReadByte(); slot != 0xFF; slot = r.ReadByte())
        {
            masked.Add((slot, r.ReadInt()));
        }

        int sticker = r.ReadInt();
        r.ReadInt(); r.ReadLong(); // pets
        Assert.Equal(0, r.Remaining);
        return (visible, masked, sticker);
    }

    [Fact]
    public void CashOverlay_TakesTheVisibleSlot_AndMasksTheBaseEquip()
    {
        var c = new Character { Id = 1, Name = "Vain" };
        c.EquippedItems.Add(new InventoryItem { ItemId = 1040002, Position = -5 });   // base top
        c.EquippedItems.Add(new InventoryItem { ItemId = 1052999, Position = -105 }); // cash top over it
        c.EquippedItems.Add(new InventoryItem { ItemId = 1072001, Position = -7 });   // shoes, uncovered

        (Dictionary<byte, int> visible, List<(byte Slot, int ItemId)> masked, int sticker) = Decode(c);

        Assert.Equal(1052999, visible[5]);          // the overlay is what renders
        Assert.Equal(1072001, visible[7]);          // uncovered base stays visible
        Assert.Contains((5, 1040002), masked.Select(m => ((int)m.Slot, m.ItemId))); // covered base masked
        Assert.Equal(0, sticker);
    }

    [Fact]
    public void CashWeapon_BecomesTheWeaponSticker()
    {
        var c = new Character { Id = 1, Name = "Sword" };
        c.EquippedItems.Add(new InventoryItem { ItemId = 1302000, Position = -11 });  // base weapon
        c.EquippedItems.Add(new InventoryItem { ItemId = 1702001, Position = -111 }); // cash weapon skin

        (Dictionary<byte, int> visible, List<(byte Slot, int ItemId)> masked, int sticker) = Decode(c);

        Assert.Equal(1302000, visible[11]);  // the base weapon stays the visible entry
        Assert.Empty(masked);                // -111 never joins the masked list
        Assert.Equal(1702001, sticker);      // it rides as the sticker int instead
    }
}
