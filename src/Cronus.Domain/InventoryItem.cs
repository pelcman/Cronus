namespace Cronus.Domain;

/// <summary>
/// One inventory/equipped item. Covers what the JMS v186 item encoders need. Equip-only fields
/// stay at their defaults for bundle items (use/setup/etc/cash). Positions are MapleStory slots:
/// negative for equipped (e.g. -5 top, -11 weapon), positive for inventory slots.
/// </summary>
public sealed class InventoryItem
{
    public int Id { get; set; }

    public int CharacterId { get; set; }

    public required int ItemId { get; set; }

    /// <summary>Slot position (negative = equipped, positive = inventory).</summary>
    public short Position { get; set; }

    /// <summary>Stack quantity (bundle items); 1 for equips.</summary>
    public short Quantity { get; set; } = 1;

    public string Owner { get; set; } = string.Empty;

    /// <summary>Item flags (sealed, tradeable, etc.).</summary>
    public short Flag { get; set; }

    // --- Equip-only stats ---
    public byte UpgradeSlots { get; set; }
    public byte Level { get; set; }
    public short Str { get; set; }
    public short Dex { get; set; }
    public short Int { get; set; }
    public short Luk { get; set; }
    public short Hp { get; set; }
    public short Mp { get; set; }
    public short Watk { get; set; }
    public short Matk { get; set; }
    public short Wdef { get; set; }
    public short Mdef { get; set; }
    public short Acc { get; set; }
    public short Avoid { get; set; }
    public short Hands { get; set; }
    public short Speed { get; set; }
    public short Jump { get; set; }
    public byte Enhance { get; set; }
    public byte PotentialRank { get; set; }
    public short Potential1 { get; set; }
    public short Potential2 { get; set; }
    public short Potential3 { get; set; }
    public int Durability { get; set; } = -1;
    public int ViciousHammer { get; set; }

    /// <summary>True if this item occupies an equipped (negative-position) slot.</summary>
    public bool IsEquipped => Position < 0;

    /// <summary>True if this item is an equip type (item id 1xxxxxx).</summary>
    public bool IsEquip => ItemId / 1_000_000 == 1;
}
