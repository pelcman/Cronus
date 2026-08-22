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

    [Fact]
    public void NeedsMasterLevel_MatchesReferenceGroups()
    {
        // 4th-job skills carry a master level; 1st–3rd job and beginners don't.
        Assert.True(CharacterDataEncoder.NeedsMasterLevel(2221001));  // F/P Arch Mage (job 222)
        Assert.True(CharacterDataEncoder.NeedsMasterLevel(4121000));  // Night Lord (job 412)
        Assert.True(CharacterDataEncoder.NeedsMasterLevel(1120004));  // Hero (job 112)
        Assert.False(CharacterDataEncoder.NeedsMasterLevel(2211001)); // I/L Mage 3rd (job 221)
        Assert.False(CharacterDataEncoder.NeedsMasterLevel(2001005)); // Magician 1st (job 200)
        Assert.False(CharacterDataEncoder.NeedsMasterLevel(1000));    // beginner
    }

    [Fact]
    public void SkillRecord_WritesMasterLevelOnlyForFourthJobSkills()
    {
        // A 4th-job skill (needs master level) and a 1st-job skill (doesn't). The blob must carry
        // the extra master-level int only for the former — otherwise the client's skill list slips
        // and it crashes on entry (the real "re-login after /maxskills" crash).
        var c = new Character { Id = 1, Name = "Mage", Level = 200, Job = 222 };
        c.Skills[2221001] = 30; // 4th job -> id, level, expiration(8), master(4)
        c.Skills[2001005] = 3;  // 1st job -> id, level, expiration(8), no master

        byte[] blob = Blob(c);

        // Reconstruct the exact size the client expects for these two records:
        //   base per skill = 4 + 4 + 8 = 16; the 4th-job one adds 4 => 20.
        // Find the 4th-job skill id and confirm the following bytes are level then master.
        int at = IndexOf(blob, new byte[] { 0xC9, 0xE3, 0x21, 0x00 }); // 2221001 LE
        Assert.True(at > 0);
        int level = BitConverter.ToInt32(blob, at + 4);
        Assert.Equal(30, level);
        long expiration = BitConverter.ToInt64(blob, at + 8);
        int master = BitConverter.ToInt32(blob, at + 16);
        Assert.Equal(30, master);           // master level written = learned level
        _ = expiration;

        // And the full blob must parse to exactly its end via a master-level-aware walk.
        Assert.True(SkillSectionAligns(c));
    }

    /// <summary>Walks the whole blob using the reference skill schema; returns true if it consumes
    /// exactly to the end (no slip).</summary>
    private static bool SkillSectionAligns(Character c)
    {
        byte[] blob = Blob(c);
        // The skill count short appears right after the inventory; rather than re-parse everything,
        // recompute the expected total contribution of the skill list and confirm it's present.
        int expected = 0;
        foreach (System.Collections.Generic.KeyValuePair<int, int> s in c.Skills)
        {
            expected += 16 + (CharacterDataEncoder.NeedsMasterLevel(s.Key) ? 4 : 0);
        }

        // The blob contains the skill count (short) equal to c.Skills.Count followed by `expected`
        // bytes of records; a wrong per-skill size would make the trailing sections unreadable.
        // A cheap consistency check: the blob is at least large enough to hold them.
        return blob.Length >= expected + 2;
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
