using Cronus.Network.Packets;

namespace Cronus.Debug.Bot;

/// <summary>
/// Parses a JMS v186 item body exactly as the client reads it (ports
/// <c>DataGW_ItemSlotBase.Encode</c>'s inverse). Used to validate that the bytes the server sends
/// for an item read back cleanly — a mis-sized body slips the reader and, in the real client,
/// crashes with "error code 38" (end of file). Throws if a read runs past the packet.
/// </summary>
public static class ItemBodyParser
{
    /// <summary>Reads one item body (type byte + raw + type-specific body). Returns its item id.</summary>
    public static int Read(PacketReader r)
    {
        int type = r.ReadByte();          // 1 equip, 2/3/4/5 bundle, 3 pet
        int itemId = ReadRaw(r, out bool hasUniqueId);

        if (type == 1)
        {
            ReadEquip(r, hasUniqueId);
        }
        else if (type == 3 && itemId / 10_000 == 500)
        {
            ReadPet(r);
        }
        else
        {
            ReadBundle(r, itemId);
        }

        return itemId;
    }

    private static int ReadRaw(PacketReader r, out bool hasUniqueId)
    {
        int itemId = r.ReadInt();
        hasUniqueId = r.ReadByte() != 0;
        if (hasUniqueId)
        {
            r.ReadLong();                 // unique id
        }

        r.ReadLong();                     // expiration (no JMS >= 194 extra int for v186)
        return itemId;
    }

    private static void ReadEquip(PacketReader r, bool hasUniqueId)
    {
        r.ReadByte();                     // upgrade slots
        r.ReadByte();                     // level
        for (int i = 0; i < 15; i++)
        {
            r.ReadShort();                // str..jump (15 stat shorts)
        }

        r.ReadString();                   // owner
        r.ReadShort();                    // flag
        r.ReadByte(); r.ReadByte(); r.ReadInt();   // reverse-weapon: levelUpType, level, exp
        r.ReadInt();                      // durability
        r.ReadInt();                      // vicious hammer
        r.ReadByte(); r.ReadByte();       // potential rank, enhance
        r.ReadShort(); r.ReadShort(); r.ReadShort(); // potentials 1..3
        r.ReadShort(); r.ReadShort();     // sockets 1..2
        if (!hasUniqueId)
        {
            r.ReadLong();                 // trailing serial (only when no unique id)
        }

        r.ReadLong();                     // JMS >= 164 tail
        r.ReadInt();                      // -1
    }

    private static void ReadPet(PacketReader r)
    {
        r.ReadBytes(13);                  // name (fixed 13)
        r.ReadByte();                     // level
        r.ReadShort();                    // closeness
        r.ReadByte();                     // fullness
        r.ReadLong();                     // dateDead
        r.ReadShort();                    // pet attribute
        r.ReadShort();                    // pet skill
        r.ReadInt();                      // remain life
        r.ReadShort();                    // attribute (JMS >= 180)
        r.ReadByte();                     // summoned (JMS >= 186)
        r.ReadInt();                      // trailing int
    }

    private static void ReadBundle(PacketReader r, int itemId)
    {
        r.ReadShort();                    // quantity
        r.ReadString();                   // owner
        r.ReadShort();                    // flag

        int img = itemId / 10_000;
        if (img is 207 or 233 or 287 or 288 or 289)
        {
            r.ReadInt(); r.ReadShort(); r.ReadByte(); r.ReadByte(); // throwing-star / bullet tail
        }
    }
}
