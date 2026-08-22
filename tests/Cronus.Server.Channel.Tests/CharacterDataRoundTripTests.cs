using Cronus.Common;
using Cronus.Domain;
using Cronus.Network.Packets;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>
/// Full-blob round-trip: builds a rich, "kitchen-sink" character and walks the entire
/// <c>WriteAllData</c> output exactly as the client reads it — mixed-job skills (so the
/// master-level branch is exercised both ways), started quests (which carry a string),
/// completed quests, and a multi-tab inventory (equip / use / etc / cash-equip / pet).
/// A mis-sized section anywhere leaves leftover bytes or overruns; the client would
/// crash with "error code 38 (end of file)". This is the automated guard for the
/// re-login EOF class of bug across every variable-length section at once.
/// </summary>
public class CharacterDataRoundTripTests
{
    [Fact]
    public void KitchenSinkCharacter_FullBlobConsumesExactly()
    {
        var c = new Character { Id = 42, Name = "Sink", Level = 200, Job = 222, Meso = 1234567 };

        // Skills spanning beginner / 1st..3rd / 4th job so both master-level branches fire.
        c.Skills[8]       = 1;   // beginner (no master)
        c.Skills[2001005] = 3;   // 1st job magician (no master)
        c.Skills[2211001] = 20;  // 3rd job I/L (no master)
        c.Skills[2221001] = 30;  // 4th job (master)
        c.Skills[2221006] = 30;  // 4th job (master)
        c.Skills[4121000] = 30;  // 4th job Night Lord (master)
        c.Skills[4331002] = 20;  // dual-blade tier 433: job%10 == 3 < 4, so no master (pre-186 path)

        // Started quests carry a string value; completed quests carry a long.
        c.StartedQuests[2000] = "005000";
        c.StartedQuests[2001] = string.Empty;
        c.CompletedQuests[1000] = 130000000L;
        c.CompletedQuests[1001] = 130000001L;

        // Multi-tab inventory: equipped equip, un-equipped equip, use bundle, etc bundle,
        // a cash equip (unique id up front, no trailing serial), a pet, and throwing stars.
        c.EquippedItems.Add(new InventoryItem { ItemId = 1302000, Position = -11, Quantity = 1, UpgradeSlots = 7 }); // weapon (equipped)
        c.EquippedItems.Add(new InventoryItem { ItemId = 1040002, Position = 1, Quantity = 1 });                     // equip tab
        c.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 1, Quantity = 200 });                   // USE
        c.EquippedItems.Add(new InventoryItem { ItemId = 4000000, Position = 1, Quantity = 99 });                    // ETC
        c.EquippedItems.Add(new InventoryItem { ItemId = 1112000, Position = 2, Quantity = 1, CashId = 0x1122334455667788 }); // cash equip
        c.EquippedItems.Add(new InventoryItem { ItemId = 5000000, Position = 3, Quantity = 1, PetName = "ペット" });   // pet (cash tab)
        c.EquippedItems.Add(new InventoryItem { ItemId = 2070000, Position = 2, Quantity = 800 });                   // throwing stars (207 tail)

        // Monster Book cards ride in the blob tail as [shortId:2][level:1] entries.
        c.MonsterCards[2380000] = 1;
        c.MonsterCards[2380100] = 5;

        var w = new PacketWriter(encoding: ServerConfig.Jms186.CodePage);
        CharacterDataEncoder.WriteAllData(w, c);
        byte[] blob = w.ToArray();

        int consumed = WalkAllData(blob);
        Assert.Equal(blob.Length, consumed);
    }

    /// <summary>Walks a <c>WriteAllData</c> blob the way the client does; returns bytes consumed
    /// (equal to the blob length iff every section is correctly sized).</summary>
    private static int WalkAllData(byte[] blob)
    {
        var r = new PacketReader(blob, ServerConfig.Jms186.CodePage);
        r.ReadLong();                 // statmask
        r.ReadByte();                 // combat orders

        // WriteStat.
        r.ReadInt(); r.ReadBytes(13); r.ReadByte(); r.ReadByte(); r.ReadInt(); r.ReadInt(); r.ReadBytes(24);
        r.ReadByte(); r.ReadShort();
        for (int i = 0; i < 4; i++) r.ReadShort();
        for (int i = 0; i < 4; i++) r.ReadShort();
        r.ReadShort(); r.ReadShort();
        r.ReadInt(); r.ReadShort(); r.ReadInt();
        r.ReadInt(); r.ReadByte(); r.ReadShort();
        r.ReadLong(); r.ReadInt(); r.ReadInt(); r.ReadInt();

        r.ReadByte(); r.ReadByte();   // buddy cap, bless
        r.ReadInt();                  // meso
        r.ReadInt(); r.ReadInt(); r.ReadInt(); // pachinko

        WalkInventory(r);

        int skills = r.ReadShort();
        for (int i = 0; i < skills; i++)
        {
            int id = r.ReadInt();
            r.ReadInt();
            r.ReadLong();
            if (CharacterDataEncoder.NeedsMasterLevel(id)) r.ReadInt();
        }

        r.ReadShort();                // cooldowns
        int started = r.ReadShort();
        for (int i = 0; i < started; i++) { r.ReadShort(); r.ReadString(); }
        r.ReadShort();                // jms184 extra
        int completed = r.ReadShort();
        for (int i = 0; i < completed; i++) { r.ReadShort(); r.ReadLong(); }
        r.ReadShort();                // minigame
        r.ReadShort(); r.ReadShort(); r.ReadShort(); // rings
        for (int i = 0; i < 15; i++) r.ReadInt();    // teleport rocks
        r.ReadShort();                               // presents
        r.ReadInt(); r.ReadByte();                   // monster book cover + shrink flag
        int cards = r.ReadShort();                   // registered card entries
        for (int i = 0; i < cards; i++) { r.ReadShort(); r.ReadByte(); }
        r.ReadShort(); r.ReadShort(); r.ReadShort(); // quest info / pre-BB extra / visitor log

        return blob.Length - r.Remaining;
    }

    private static void WalkInventory(PacketReader r)
    {
        for (int i = 0; i < 5; i++) r.ReadByte();
        r.ReadInt(); r.ReadInt();

        for (int section = 0; section < 4; section++)
        {
            while (r.ReadShort() != 0) WalkItem(r);
        }

        for (int tab = 0; tab < 4; tab++)
        {
            while (r.ReadByte() != 0) WalkItem(r);
        }
    }

    private static void WalkItem(PacketReader r)
    {
        // The client dispatches on the type byte alone: 1 equip, 2 bundle, 3 pet. Anything else
        // is unparseable to it (the wrong-tab-number bug crashed the real client with EOF).
        int type = r.ReadByte();
        Assert.True(type is 1 or 2 or 3, $"item type byte {type} — client only knows 1/2/3");

        int itemId = r.ReadInt();
        bool hasUid = r.ReadByte() != 0;
        if (hasUid) r.ReadLong();
        r.ReadLong();                 // expiration

        if (type == 1)
        {
            r.ReadByte(); r.ReadByte();
            for (int i = 0; i < 15; i++) r.ReadShort();
            r.ReadString(); r.ReadShort();
            r.ReadByte(); r.ReadByte(); r.ReadInt();
            r.ReadInt(); r.ReadInt();
            r.ReadByte(); r.ReadByte();
            r.ReadShort(); r.ReadShort(); r.ReadShort();
            r.ReadShort(); r.ReadShort();
            if (!hasUid) r.ReadLong();
            r.ReadLong(); r.ReadInt();
        }
        else if (type == 3)
        {
            r.ReadBytes(13); r.ReadByte(); r.ReadShort(); r.ReadByte(); r.ReadLong();
            r.ReadShort(); r.ReadShort(); r.ReadInt(); r.ReadShort(); r.ReadByte(); r.ReadInt();
        }
        else
        {
            r.ReadShort(); r.ReadString(); r.ReadShort();
            int img = itemId / 10_000;
            if (img is 207 or 233 or 287 or 288 or 289)
            {
                r.ReadInt(); r.ReadShort(); r.ReadByte(); r.ReadByte();
            }
        }
    }
}
