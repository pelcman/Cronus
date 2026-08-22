using System.Linq;
using Cronus.Domain;
using Cronus.Network.Packets;
using Cronus.Server.Login;

namespace Cronus.Server.Game;

/// <summary>
/// Serializes the full CharacterData blob sent inside <c>LP_SetField</c> on game entry.
/// Ports the JMS v186 path of <c>DataCharacterData.Encode(chr, -1)</c> — pre-Big-Bang,
/// JMS &gt;= 180 &amp;&amp; &lt; 187 — with empty inventories, skills, quests, rings, and
/// monster book (item persistence lands later). Every conditional the Java takes for this
/// version is flattened into a straight-line write.
/// </summary>
public static class CharacterDataEncoder
{
    private const int EmptyTeleportRock = 999999999;
    private const byte DefaultInventorySlots = 24;

    /// <summary>Windows FILETIME for the current instant (100ns ticks since 1601-01-01).</summary>
    public static long FileTimeNow() => DateTime.UtcNow.ToFileTimeUtc();

    /// <summary>FILETIME for "2079-07-07" — the no-expiration sentinel (permanent skills).</summary>
    private const long NoExpiration = 151004124000000000L;

    /// <summary>Writes CharacterData(datamask = -1) for <paramref name="c"/>.</summary>
    public static void WriteAllData(PacketWriter w, Character c)
    {
        w.WriteLong(-1);                  // statmask (all)
        w.WriteByte(0);                   // nCombatOrders (JMS >= 180)

        // [0x01] character stat + buddy capacity + bless of fairy
        CharacterEncoder.WriteStat(w, c);
        w.WriteByte((byte)c.BuddyCapacity);
        w.WriteByte(0);                   // bless of fairy: none

        // [0x02] money + pachinko
        w.WriteInt(c.Meso);
        w.WriteInt(c.Id);                 // pachinko: character id
        w.WriteInt(0);                    // pachinko: tama
        w.WriteInt(0);                    // pachinko: reserved

        WriteInventoryInfo(w, c);

        // [0x100] skills: id, level, expiration (JMS >= 180), then a master-level int for skills
        // that carry one (4th-job skills raised by mastery books). Omitting that int for a
        // master-level skill slips every following byte and crashes the client (EOF) — which is
        // exactly what a /maxskills'd advanced character hit on re-entry.
        w.WriteShort((short)c.Skills.Count);
        foreach (KeyValuePair<int, int> skill in c.Skills)
        {
            w.WriteInt(skill.Key);
            w.WriteInt(skill.Value);
            w.WriteLong(NoExpiration);
            if (NeedsMasterLevel(skill.Key))
            {
                w.WriteInt(skill.Value); // master level = the learned (maxed) level
            }
        }

        // [0x8000] cooldowns (none)
        w.WriteShort(0);

        // [0x200] started quests + JMS 184-186 extra list
        w.WriteShort((short)c.StartedQuests.Count);
        foreach (KeyValuePair<int, string> quest in c.StartedQuests)
        {
            w.WriteShort((short)quest.Key);
            w.WriteString(quest.Value);
        }

        w.WriteShort(0);   // JMS 184-186 extra list

        // [0x4000] completed quests
        w.WriteShort((short)c.CompletedQuests.Count);
        foreach (KeyValuePair<int, long> quest in c.CompletedQuests)
        {
            w.WriteShort((short)quest.Key);
            w.WriteLong(quest.Value);
        }

        // [0x400] minigame records (none)
        w.WriteShort(0);

        // [0x800] rings: couple / friend / marriage (none)
        w.WriteShort(0);
        w.WriteShort(0);
        w.WriteShort(0);

        // [0x1000] teleport rocks: 5 regular + 10 VIP, all empty
        for (int i = 0; i < 15; i++)
        {
            w.WriteInt(EmptyTeleportRock);
        }

        // JMS branch tail:
        w.WriteShort(0);                  // [0x7C] presents (none)
        w.WriteInt(0);                    // [0x20000] monster book cover
        w.WriteByte(0);                   // [0x10000] monster book: not shrunk
        w.WriteShort(0);                  //           card count 0
        w.WriteShort(0);                  // [0x40000] quest info records (none)
        w.WriteShort(0);                  // [0x80000] (pre-BB extra)
        w.WriteShort(0);                  // [0x200000] visitor quest log (JMS >= 186)
    }

    /// <summary>
    /// True when a skill carries a master-level field in the skill record (ports the pre-BB path of
    /// <c>Structure.is_skill_need_master_level</c>): 4th-job skills, plus the Evan/Dual-Blade
    /// special cases. The beginner families never do. Getting this wrong shifts the skill list and
    /// crashes the client with EOF.
    /// </summary>
    public static bool NeedsMasterLevel(int skillId)
    {
        int jobId = skillId / 10000;

        // Evan (2200–2218): master level from the 7th tier up.
        if (jobId is >= 2200 and <= 2218)
        {
            return jobId % 10 >= 7;
        }

        // Dual Blade (430–434): from the 4th tier up.
        if (jobId is >= 430 and <= 434)
        {
            return jobId % 10 >= 4;
        }

        // Beginner families never carry a master level.
        if (jobId is 0 or 1000 or 2000 or 2001 or 3000)
        {
            return false;
        }

        // Otherwise the 4th-job skills (job tier digit >= 2).
        return jobId % 10 >= 2;
    }

    /// <summary>
    /// Writes InventoryInfo(datamask = -1) (JMS v186 layout, ports <c>DataCharacterData.InventoryInfo</c>):
    /// equipped items, then each inventory tab's items (EQUIP / USE / SETUP / ETC / CASH), each section
    /// closed by its terminator (2 bytes for equip-typed sections, 1 byte for the bundle tabs). Items
    /// are keyed by slot: negative positions are equipped, positive positions live in a tab chosen by
    /// the item id's type digit.
    /// </summary>
    private static void WriteInventoryInfo(PacketWriter w, Character c)
    {
        // [0x80] slot limits: EQUIP / USE / SETUP / ETC / CASH
        for (int i = 0; i < 5; i++)
        {
            w.WriteByte(DefaultInventorySlots);
        }

        // [0x100000] (JMS >= 165)
        w.WriteInt(0);
        w.WriteInt(0);

        // [0x04] equipped items (positions -1..-99), sorted by slot, then the terminator.
        foreach (InventoryItem item in c.EquippedItems
                     .Where(i => i.Position is > -100 and < 0)
                     .OrderBy(i => -i.Position))
        {
            ItemEncoder.WriteSlot(w, item);
            ItemEncoder.WriteItem(w, item);
        }

        w.WriteShort(0);              // end of equipped items
        w.WriteShort(0);              // end of equipped avatars (none)

        WriteTab(w, c, type: 1);      // EQUIP tab (un-equipped equips, positive slots)
        w.WriteShort(0);              // end of equip inventory

        w.WriteShort(0);              // end of JMS >= 180 (-1000..) block (none)

        // [0x08/0x10/0x20/0x40] USE, SETUP, ETC, CASH — each a run of items then a 1-byte terminator.
        for (int type = 2; type <= 5; type++)
        {
            WriteTab(w, c, type);
            w.WriteByte(0);
        }
    }

    /// <summary>Writes the items in one inventory tab: positive slots whose id-type matches, by slot.</summary>
    private static void WriteTab(PacketWriter w, Character c, int type)
    {
        foreach (InventoryItem item in c.EquippedItems
                     .Where(i => i.Position > 0 && ItemEncoder.ItemType(i.ItemId) == type)
                     .OrderBy(i => i.Position))
        {
            ItemEncoder.WriteSlot(w, item);
            ItemEncoder.WriteItem(w, item);
        }
    }
}
