using Cronus.Common;
using Cronus.Domain;
using Cronus.Network.Packets;
using Cronus.Server.Login;
using Xunit;

namespace Cronus.Server.Login.Tests;

public class ItemEncoderTests
{
    private static PacketReader Encode(InventoryItem item)
    {
        var w = new PacketWriter(encoding: ServerConfig.Jms186.CodePage);
        ItemEncoder.WriteSlot(w, item);
        ItemEncoder.WriteItem(w, item);
        return new PacketReader(w.ToArray(), ServerConfig.Jms186.CodePage);
    }

    [Fact]
    public void Equip_HasExactJmsV186Layout()
    {
        var weapon = new InventoryItem
        {
            ItemId = 1302000, // a sword (equip)
            Position = -11,    // weapon slot
            UpgradeSlots = 7,
            Str = 5,
            Watk = 17,
            Durability = -1,
        };

        PacketReader r = Encode(weapon);

        // EncodeSlot: equip type -> 2-byte slot; -11 normalizes to 11.
        Assert.Equal(11, r.ReadShort());
        // Body: type byte then RawEncode.
        Assert.Equal(1, r.ReadByte());               // type = equip
        Assert.Equal(1302000, r.ReadInt());          // item id
        Assert.Equal(0, r.ReadByte());               // hasUniqueId
        Assert.Equal(ItemEncoder.NoExpiration, r.ReadLong());
        // Equip stats.
        Assert.Equal(7, r.ReadByte());               // upgrade slots
        Assert.Equal(0, r.ReadByte());               // level
        Assert.Equal(5, r.ReadShort());              // str
        r.Skip(2 * 3);                               // dex/int/luk
        r.Skip(2 * 2);                               // hp/mp
        Assert.Equal(17, r.ReadShort());             // watk
        r.Skip(2 * 8);                               // matk,wdef,mdef,acc,avoid,hands,speed,jump
        Assert.Equal(string.Empty, r.ReadString());  // owner
        Assert.Equal(0, r.ReadShort());              // flag
        // Reverse-weapon block.
        Assert.Equal(0, r.ReadByte());               // levelUpType
        Assert.Equal(0, r.ReadByte());               // item level
        Assert.Equal(0, r.ReadInt());                // exp
        Assert.Equal(-1, r.ReadInt());               // durability
        Assert.Equal(0, r.ReadInt());                // vicious hammer
        // Potential block.
        Assert.Equal(0, r.ReadByte());               // potential rank
        Assert.Equal(0, r.ReadByte());               // enhance
        r.Skip(2 * 3);                               // pot1..3
        r.Skip(2 * 2);                               // sockets
        Assert.Equal(0, r.ReadLong());               // no-uid tail
        Assert.Equal(0, r.ReadLong());               // JMS>=164 tail
        Assert.Equal(-1, r.ReadInt());
        Assert.Equal(0, r.Remaining);                // exact layout
    }

    [Fact]
    public void Bundle_HasExactLayout()
    {
        var potion = new InventoryItem
        {
            ItemId = 2000000, // a use item (bundle)
            Position = 3,
            Quantity = 200,
        };

        PacketReader r = Encode(potion);

        Assert.Equal(3, r.ReadByte());       // 1-byte slot for non-equip
        Assert.Equal(2, r.ReadByte());       // type = use
        Assert.Equal(2000000, r.ReadInt());  // item id
        Assert.Equal(0, r.ReadByte());       // hasUniqueId
        Assert.Equal(ItemEncoder.NoExpiration, r.ReadLong());
        Assert.Equal(200, r.ReadShort());    // quantity
        Assert.Equal(string.Empty, r.ReadString());
        Assert.Equal(0, r.ReadShort());      // flag
        Assert.Equal(0, r.Remaining);
    }

    [Fact]
    public void AvatarLook_RendersEquippedItems()
    {
        var c = new Character { Name = "Dressed" };
        c.EquippedItems.Add(new InventoryItem { ItemId = 1040002, Position = -5 });  // top
        c.EquippedItems.Add(new InventoryItem { ItemId = 1302000, Position = -11 }); // weapon

        var w = new PacketWriter(encoding: ServerConfig.Jms186.CodePage);
        CharacterEncoder.WriteAvatarLook(w, c);
        var r = new PacketReader(w.ToArray(), ServerConfig.Jms186.CodePage);

        r.ReadByte();  // gender
        r.ReadByte();  // skin
        r.ReadInt();   // face
        r.ReadByte();  // ignored
        r.ReadInt();   // hair

        // Visible equips sorted by slot: 5 (top), 11 (weapon).
        Assert.Equal(5, r.ReadByte());
        Assert.Equal(1040002, r.ReadInt());
        Assert.Equal(11, r.ReadByte());
        Assert.Equal(1302000, r.ReadInt());
        Assert.Equal(0xFF, r.ReadByte()); // end visible
        Assert.Equal(0xFF, r.ReadByte()); // end masked
    }

    [Fact]
    public void CashEquip_WritesUniqueIdUpFront_AndOmitsTheTrailingSerial()
    {
        // A cash equip carries its 8-byte unique id at the front (hasUniqueId = 1). The reference
        // then OMITS the trailing "no serial" long, so a cash equip's body is exactly 8 bytes
        // shorter than the same non-cash equip — otherwise the client's parse of the next item
        // slips 8 bytes and crashes with EOF (error 38).
        var cashHat = new InventoryItem { ItemId = 1002077, Position = 1, CashId = 0x1122334455667788L };
        var plainHat = new InventoryItem { ItemId = 1002077, Position = 1 };

        byte[] cashBytes = EncodeToArray(cashHat);
        byte[] plainBytes = EncodeToArray(plainHat);

        // +8 for the up-front unique id, -8 for the omitted trailing serial => same total length.
        Assert.Equal(plainBytes.Length, cashBytes.Length);

        PacketReader r = Encode(cashHat);
        r.ReadShort();                                   // slot
        Assert.Equal(1, r.ReadByte());                   // type = equip
        Assert.Equal(1002077, r.ReadInt());              // item id
        Assert.Equal(1, r.ReadByte());                   // hasUniqueId = 1
        Assert.Equal(0x1122334455667788L, r.ReadLong()); // the cash unique id, up front
        Assert.Equal(ItemEncoder.NoExpiration, r.ReadLong());

        // Body: skip to the tail and confirm it ends cleanly (no stray 8-byte serial).
        r.ReadByte(); r.ReadByte();                      // upgrade slots, level
        for (int i = 0; i < 15; i++) { r.ReadShort(); }  // 15 stat shorts
        r.ReadString();                                  // owner
        r.ReadShort();                                   // flag
        r.ReadByte(); r.ReadByte(); r.ReadInt();         // reverse-weapon block
        r.ReadInt(); r.ReadInt();                        // durability, vicious hammer
        r.ReadByte(); r.ReadByte();                      // potential rank, enhance
        r.ReadShort(); r.ReadShort(); r.ReadShort();     // potentials
        r.ReadShort(); r.ReadShort();                    // sockets
        // No trailing no-serial long here (that's the fix).
        Assert.Equal(0, r.ReadLong());                   // JMS>=164 tail
        Assert.Equal(-1, r.ReadInt());
        Assert.Equal(0, r.Remaining);                    // exact — no extra 8 bytes
    }

    private static byte[] EncodeToArray(InventoryItem item)
    {
        var w = new PacketWriter(encoding: ServerConfig.Jms186.CodePage);
        ItemEncoder.WriteSlot(w, item);
        ItemEncoder.WriteItem(w, item);
        return w.ToArray();
    }
}
