using Cronus.Domain;
using Cronus.Network.Packets;

namespace Cronus.Server.Login;

/// <summary>
/// Serializes inventory items for JMS v186 (ports <c>DataGW_ItemSlotBase</c>: RawEncode plus
/// the equip and bundle bodies, pre-Big-Bang, JMS &gt;= 186 &amp;&amp; &lt; 187). Unique-id and
/// cash/pet items are not modeled yet (hasUniqueId is always 0).
/// </summary>
public static class ItemEncoder
{
    /// <summary>FILETIME for "2079-07-07 07:00:00" — the no-expiration sentinel for non-pet items.</summary>
    public const long NoExpiration = 151004124000000000L;

    /// <summary>Inventory type from an item id (1 equip, 2 use, 3 setup, 4 etc, 5 cash).</summary>
    public static int ItemType(int itemId) => itemId / 1_000_000;

    /// <summary>
    /// Writes the item's slot position (<c>EncodeSlot</c>): 2 bytes for equip-type items on
    /// v186+, otherwise 1 byte. Equipped negative positions are normalized to a positive slot.
    /// </summary>
    public static void WriteSlot(PacketWriter w, InventoryItem item)
    {
        int pos = item.Position;
        if (pos <= -1)
        {
            pos = -pos;
            if (pos > 100 && pos < 1000)
            {
                pos -= 100;
            }
        }

        if (ItemType(item.ItemId) == 1)
        {
            w.WriteShort((short)pos);
        }
        else
        {
            w.WriteByte((byte)pos);
        }
    }

    /// <summary>Writes the item body (<c>GW_ItemSlotBase::Encode</c>): type byte, RawEncode, then equip/bundle.</summary>
    public static void WriteItem(PacketWriter w, InventoryItem item)
    {
        int type = ItemType(item.ItemId);
        w.WriteByte((byte)type);

        WriteRaw(w, item);

        if (type == 1)
        {
            WriteEquipBody(w, item);
        }
        else
        {
            WriteBundleBody(w, item);
        }
    }

    private static void WriteRaw(PacketWriter w, InventoryItem item)
    {
        w.WriteInt(item.ItemId);
        w.WriteByte(0);                 // hasUniqueId = 0
        w.WriteLong(NoExpiration);
        // JMS >= 194 would add Encode4(0) here; v186 does not.
    }

    private static void WriteEquipBody(PacketWriter w, InventoryItem e)
    {
        w.WriteByte(e.UpgradeSlots);
        w.WriteByte(e.Level);
        w.WriteShort(e.Str);
        w.WriteShort(e.Dex);
        w.WriteShort(e.Int);
        w.WriteShort(e.Luk);
        w.WriteShort(e.Hp);
        w.WriteShort(e.Mp);
        w.WriteShort(e.Watk);
        w.WriteShort(e.Matk);
        w.WriteShort(e.Wdef);
        w.WriteShort(e.Mdef);
        w.WriteShort(e.Acc);
        w.WriteShort(e.Avoid);
        w.WriteShort(e.Hands);
        w.WriteShort(e.Speed);
        w.WriteShort(e.Jump);
        w.WriteString(e.Owner);
        w.WriteShort(e.Flag);

        // Reverse-weapon block (JMS >= 164).
        w.WriteByte(0);                 // levelUpType
        w.WriteByte(0);                 // item level (baseLevel/equipLevel)
        w.WriteInt(0);                  // exp

        w.WriteInt(e.Durability);       // JMS >= 180
        w.WriteInt(e.ViciousHammer);    // JMS >= 180

        // Potential / enhancement (JMS >= 186).
        w.WriteByte(e.PotentialRank);
        w.WriteByte(e.Enhance);
        w.WriteShort(e.Potential1);
        w.WriteShort(e.Potential2);
        w.WriteShort(e.Potential3);
        w.WriteShort(0);                // socket 1
        w.WriteShort(0);                // socket 2

        w.WriteLong(0);                 // no-unique-id tail
        // JMS >= 164 tail.
        w.WriteLong(0);
        w.WriteInt(-1);
    }

    private static void WriteBundleBody(PacketWriter w, InventoryItem item)
    {
        w.WriteShort(item.Quantity);
        w.WriteString(item.Owner);
        w.WriteShort(item.Flag);

        // Throwing stars / bullets carry an 8-byte tail.
        int imgNum = item.ItemId / 10_000;
        if (imgNum is 207 or 233 or 287 or 288 or 289)
        {
            w.WriteInt(2);
            w.WriteShort(0x54);
            w.WriteByte(0);
            w.WriteByte(0x34);
        }
    }
}
