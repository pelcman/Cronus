using Cronus.Network.Packets;

namespace Cronus.Debug.Bot;

/// <summary>
/// Parses the full CharacterData blob inside an entry <c>LP_SetField</c> exactly as the client
/// reads it (ports <c>DataCharacterData</c>, JMS v186), so a mis-sized section — the "error code
/// 38 / re-login crash" class — is caught: the parse either overruns (throws) or leaves leftover
/// bytes. Covers stat, money, the inventory (via <see cref="ItemBodyParser"/>), master-level-aware
/// skills, quests, rings, teleport rocks, and the JMS tail.
/// </summary>
public static class CharacterDataParser
{
    /// <summary>Reads the SetField entry packet; throws or returns leftover-byte count.</summary>
    public static int ValidateSetField(PacketReader r)
    {
        // SetFieldEnterGame prefix.
        r.ReadShort();                 // ClientOptMan (none)
        r.ReadInt();                   // channel id
        r.ReadByte();
        r.ReadInt();                   // old driver id
        r.ReadByte();                  // portal count
        byte hasCharData = r.ReadByte();
        r.ReadShort();                 // notifier check
        if (hasCharData != 1)
        {
            return r.Remaining;        // a map-change SetField, not the full blob
        }

        r.ReadInt(); r.ReadInt(); r.ReadInt(); // damage seeds

        ReadCharacterData(r);

        r.ReadInt(); r.ReadInt(); r.ReadInt(); r.ReadInt(); // logout-gift config
        r.ReadLong();                  // ftServer
        return r.Remaining;
    }

    /// <summary>Reads the cash-shop entry packet (<c>LP_SetCashShop</c>), which embeds the same
    /// full CharacterData blob followed by the account name and the (empty) sale tables. Throws or
    /// returns leftover-byte count — a maxed character crashed here for the same reason as entry.</summary>
    public static int ValidateSetCashShop(PacketReader r)
    {
        ReadCharacterData(r);

        r.ReadString();               // maple id / account name
        r.ReadShort();                // sale-info: no overridden commodities
        r.ReadShort();                // modified-commodity count (JMS >= 180)
        r.ReadByte();                 // discount-rate count
        r.ReadBytes(1080);            // best items table
        r.ReadShort();                // stock
        r.ReadShort();                // limit goods
        r.ReadByte();                 // event flag
        return r.Remaining;
    }

    private static void ReadCharacterData(PacketReader r)
    {
        r.ReadLong();                  // statmask
        r.ReadByte();                  // combat orders

        // WriteStat
        r.ReadInt();                   // id
        r.ReadBytes(13);               // name
        r.ReadByte(); r.ReadByte();    // gender, skin
        r.ReadInt(); r.ReadInt();      // face, hair
        r.ReadBytes(24);               // reserved
        r.ReadByte();                  // level
        r.ReadShort();                 // job
        for (int i = 0; i < 4; i++) r.ReadShort(); // str dex int luk
        for (int i = 0; i < 4; i++) r.ReadShort(); // hp maxhp mp maxmp
        r.ReadShort(); r.ReadShort();  // ap sp
        r.ReadInt(); r.ReadShort(); r.ReadInt(); // exp fame gashaexp
        r.ReadInt(); r.ReadByte(); r.ReadShort(); // map portal subcat
        r.ReadLong(); r.ReadInt(); r.ReadInt(); r.ReadInt(); // tail

        r.ReadByte();                  // buddy capacity
        r.ReadByte();                  // bless of fairy
        r.ReadInt();                   // meso
        r.ReadInt(); r.ReadInt(); r.ReadInt(); // pachinko

        ReadInventory(r);

        // Skills (master-level-aware).
        int skills = r.ReadShort();
        for (int i = 0; i < skills; i++)
        {
            int id = r.ReadInt();
            r.ReadInt();               // level
            r.ReadLong();              // expiration
            if (NeedsMasterLevel(id))
            {
                r.ReadInt();           // master level
            }
        }

        r.ReadShort();                 // cooldowns

        int started = r.ReadShort();
        for (int i = 0; i < started; i++) { r.ReadShort(); r.ReadString(); }
        r.ReadShort();                 // jms184 extra

        int completed = r.ReadShort();
        for (int i = 0; i < completed; i++) { r.ReadShort(); r.ReadLong(); }

        r.ReadShort();                 // minigame
        r.ReadShort(); r.ReadShort(); r.ReadShort(); // rings
        for (int i = 0; i < 15; i++) r.ReadInt();    // teleport rocks

        r.ReadShort();                 // presents
        r.ReadInt();                   // monster book cover
        r.ReadByte();                  // monster book shrink
        r.ReadShort();                 // card count
        r.ReadShort();                 // quest info
        r.ReadShort();                 // pre-BB extra
        r.ReadShort();                 // visitor quest log
    }

    private static void ReadInventory(PacketReader r)
    {
        for (int i = 0; i < 5; i++) r.ReadByte(); // slot limits
        r.ReadInt(); r.ReadInt();      // 0x100000 block

        // Equip: equipped, avatars, equip-inventory, jms180 -1000 — each short-terminated.
        for (int section = 0; section < 4; section++)
        {
            while (true)
            {
                short slot = r.ReadShort();
                if (slot == 0) break;
                ItemBodyParser.Read(r);
            }
        }

        // USE / SETUP / ETC / CASH — 1-byte slot, 1-byte terminator.
        for (int tab = 0; tab < 4; tab++)
        {
            while (true)
            {
                int slot = r.ReadByte();
                if (slot == 0) break;
                ItemBodyParser.Read(r);
            }
        }
    }

    /// <summary>Master-level condition (pre-BB path of the reference) — must match the server.</summary>
    public static bool NeedsMasterLevel(int skillId)
    {
        int jobId = skillId / 10000;
        if (jobId is >= 2200 and <= 2218) return jobId % 10 >= 7;
        if (jobId is >= 430 and <= 434) return jobId % 10 >= 4;
        if (jobId is 0 or 1000 or 2000 or 2001 or 3000) return false;
        return jobId % 10 >= 2;
    }
}
