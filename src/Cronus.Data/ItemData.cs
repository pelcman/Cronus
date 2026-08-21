using System.Collections.Concurrent;

namespace Cronus.Data;

/// <summary>
/// A consumable's use effect and stack limit, from <c>Item.wz/Consume/{prefix}.img/{id}/spec</c>
/// and <c>/info/slotMax</c>. Only the fields Cronus acts on (HP/MP recovery, flat and %) are kept.
/// </summary>
public sealed class ConsumeSpec
{
    public required int ItemId { get; init; }

    /// <summary>Flat HP restored.</summary>
    public int Hp { get; init; }

    /// <summary>Flat MP restored.</summary>
    public int Mp { get; init; }

    /// <summary>Percent of max HP restored.</summary>
    public int HpRate { get; init; }

    /// <summary>Percent of max MP restored.</summary>
    public int MpRate { get; init; }

    /// <summary>
    /// Target map for a return/teleport scroll (wz <c>spec/moveTo</c>); 0 = none,
    /// 999999999 = the current map's return field.
    /// </summary>
    public int MoveTo { get; init; }

    /// <summary>Weapon-attack buff — wz <c>spec/pad</c>.</summary>
    public int Pad { get; init; }

    /// <summary>Magic-attack buff — wz <c>spec/mad</c>.</summary>
    public int Mad { get; init; }

    /// <summary>Weapon-defense buff — wz <c>spec/pdd</c>.</summary>
    public int Pdd { get; init; }

    /// <summary>Magic-defense buff — wz <c>spec/mdd</c>.</summary>
    public int Mdd { get; init; }

    /// <summary>Accuracy buff — wz <c>spec/acc</c>.</summary>
    public int Acc { get; init; }

    /// <summary>Avoidability buff — wz <c>spec/eva</c>.</summary>
    public int Eva { get; init; }

    /// <summary>Speed buff — wz <c>spec/speed</c>.</summary>
    public int Speed { get; init; }

    /// <summary>Jump buff — wz <c>spec/jump</c>.</summary>
    public int Jump { get; init; }

    /// <summary>Buff duration in milliseconds — wz <c>spec/time</c> (already stored in ms).</summary>
    public int Time { get; init; }

    /// <summary>True when this consumable grants a temporary stat buff.</summary>
    public bool IsBuff => Time > 0 && (Pad | Mad | Pdd | Mdd | Acc | Eva | Speed | Jump) != 0;

    /// <summary>Maximum stack size for this item (default 100).</summary>
    public int SlotMax { get; init; } = 100;
}

/// <summary>
/// An equip's base stats, from the equip's <c>info</c> node in the Character tree
/// (<c>Character/{Folder}/{id:00000000}.img.xml</c>). Each field maps to a wz <c>inc*</c> key
/// (plus <c>tuc</c> for upgrade slots); any key absent from the node contributes 0. Types are
/// chosen to assign cleanly onto an <c>InventoryItem</c> (<c>byte</c> slots, <c>short</c> stats).
/// </summary>
public sealed record EquipStats
{
    /// <summary>Available upgrade (scroll) slots — wz <c>tuc</c>.</summary>
    public byte UpgradeSlots { get; init; }

    /// <summary>wz <c>incSTR</c>.</summary>
    public short Str { get; init; }

    /// <summary>wz <c>incDEX</c>.</summary>
    public short Dex { get; init; }

    /// <summary>wz <c>incINT</c>.</summary>
    public short Int { get; init; }

    /// <summary>wz <c>incLUK</c>.</summary>
    public short Luk { get; init; }

    /// <summary>wz <c>incMHP</c>.</summary>
    public short Hp { get; init; }

    /// <summary>wz <c>incMMP</c>.</summary>
    public short Mp { get; init; }

    /// <summary>Weapon attack — wz <c>incPAD</c>.</summary>
    public short Watk { get; init; }

    /// <summary>Magic attack — wz <c>incMAD</c>.</summary>
    public short Matk { get; init; }

    /// <summary>Weapon defense — wz <c>incPDD</c>.</summary>
    public short Wdef { get; init; }

    /// <summary>Magic defense — wz <c>incMDD</c>.</summary>
    public short Mdef { get; init; }

    /// <summary>Accuracy — wz <c>incACC</c>.</summary>
    public short Acc { get; init; }

    /// <summary>Avoidability — wz <c>incEVA</c>.</summary>
    public short Avoid { get; init; }

    /// <summary>Hands — wz <c>incHANDS</c> (0 when absent).</summary>
    public short Hands { get; init; }

    /// <summary>wz <c>incSPEED</c>.</summary>
    public short Speed { get; init; }

    /// <summary>wz <c>incJUMP</c>.</summary>
    public short Jump { get; init; }
}

/// <summary>Provides item metadata (consumable specs, prices, equip base stats) by item id.</summary>
public interface IItemProvider
{
    ConsumeSpec? GetConsume(int itemId);

    /// <summary>The item's wz <c>info/price</c>, or null when unknown / not found.</summary>
    int? GetPrice(int itemId);

    /// <summary>
    /// An equip's base stats from its <c>info</c> node (only when <c>itemId / 1000000 == 1</c>);
    /// null for non-equips or when no equip file is found.
    /// </summary>
    EquipStats? GetEquipStats(int itemId);

    /// <summary>The per-unit price of a rechargeable stack (wz <c>info/unitPrice</c>), or null.</summary>
    double? GetUnitPrice(int itemId);
}

/// <summary>
/// Loads consumable specs from a wz_xml tree: <c>Item/Consume/{itemId/10000:0000}.img.xml</c>,
/// then the <c>{itemId:00000000}</c> node's <c>spec</c> / <c>info</c> (cached). Missing → null.
/// </summary>
public sealed class WzItemProvider : IItemProvider
{
    /// <summary>
    /// The Character-tree subfolders that hold equips, one <c>{id:00000000}.img.xml</c> per equip.
    /// The right folder for an id is found by search (see <see cref="ResolveEquipPath"/>) rather than
    /// a brittle id-range table.
    /// </summary>
    private static readonly string[] EquipFolders =
    {
        "Accessory", "Cap", "Cape", "Coat", "Dragon", "Glove", "Longcoat",
        "Pants", "PetEquip", "Ring", "Shield", "Shoes", "TamingMob", "Weapon",
    };

    private readonly string _wzRoot;
    private readonly ConcurrentDictionary<int, ConsumeSpec?> _cache = new();
    private readonly ConcurrentDictionary<int, int?> _priceCache = new();
    private readonly ConcurrentDictionary<int, EquipStats?> _equipCache = new();
    private readonly ConcurrentDictionary<int, string?> _equipPathCache = new();

    public WzItemProvider(string wzRoot) => _wzRoot = wzRoot;

    public ConsumeSpec? GetConsume(int itemId) => _cache.GetOrAdd(itemId, Load);

    public int? GetPrice(int itemId) => _priceCache.GetOrAdd(itemId, LoadPrice);

    public EquipStats? GetEquipStats(int itemId) => _equipCache.GetOrAdd(itemId, LoadEquip);

    private ConsumeSpec? Load(int itemId)
    {
        string path = ConsumeImagePath(_wzRoot, itemId);
        if (!File.Exists(path))
        {
            return null;
        }

        WzData? node = WzData.ParseFile(path).Child($"{itemId:00000000}");
        if (node is null)
        {
            return null;
        }

        WzData? spec = node.Child("spec");
        WzData? info = node.Child("info");
        return new ConsumeSpec
        {
            ItemId = itemId,
            Hp = spec?.GetInt("hp") ?? 0,
            Mp = spec?.GetInt("mp") ?? 0,
            HpRate = spec?.GetInt("hpR") ?? 0,
            MpRate = spec?.GetInt("mpR") ?? 0,
            MoveTo = spec?.GetInt("moveTo") ?? 0,
            Pad = spec?.GetInt("pad") ?? 0,
            Mad = spec?.GetInt("mad") ?? 0,
            Pdd = spec?.GetInt("pdd") ?? 0,
            Mdd = spec?.GetInt("mdd") ?? 0,
            Acc = spec?.GetInt("acc") ?? 0,
            Eva = spec?.GetInt("eva") ?? 0,
            Speed = spec?.GetInt("speed") ?? 0,
            Jump = spec?.GetInt("jump") ?? 0,
            Time = spec?.GetInt("time") ?? 0,
            SlotMax = info?.GetInt("slotMax", 100) ?? 100,
        };
    }

    private int? LoadPrice(int itemId)
    {
        string? path = ItemImagePath(_wzRoot, itemId);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        WzData? info = WzData.ParseFile(path).Child($"{itemId:00000000}")?.Child("info");
        return info?.Child("price")?.AsInt();
    }

    private readonly ConcurrentDictionary<int, double?> _unitPriceCache = new();

    public double? GetUnitPrice(int itemId) => _unitPriceCache.GetOrAdd(itemId, LoadUnitPrice);

    private double? LoadUnitPrice(int itemId)
    {
        string? path = ItemImagePath(_wzRoot, itemId);
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        WzData? info = WzData.ParseFile(path).Child($"{itemId:00000000}")?.Child("info");
        return info?.Child("unitPrice")?.AsDouble();
    }

    private EquipStats? LoadEquip(int itemId)
    {
        if (itemId / 1000000 != 1)
        {
            return null;
        }

        string? path = _equipPathCache.GetOrAdd(itemId, ResolveEquipPath);
        if (path is null)
        {
            return null;
        }

        // Each equip file's root imgdir contains the info node directly (unlike grouped Consume
        // files, which nest each item under an {id:00000000} node).
        WzData? info = WzData.ParseFile(path).Child("info");
        if (info is null)
        {
            return null;
        }

        return new EquipStats
        {
            UpgradeSlots = (byte)info.GetInt("tuc"),
            Str = (short)info.GetInt("incSTR"),
            Dex = (short)info.GetInt("incDEX"),
            Int = (short)info.GetInt("incINT"),
            Luk = (short)info.GetInt("incLUK"),
            Hp = (short)info.GetInt("incMHP"),
            Mp = (short)info.GetInt("incMMP"),
            Watk = (short)info.GetInt("incPAD"),
            Matk = (short)info.GetInt("incMAD"),
            Wdef = (short)info.GetInt("incPDD"),
            Mdef = (short)info.GetInt("incMDD"),
            Acc = (short)info.GetInt("incACC"),
            Avoid = (short)info.GetInt("incEVA"),
            Hands = (short)info.GetInt("incHANDS"),
            Speed = (short)info.GetInt("incSPEED"),
            Jump = (short)info.GetInt("incJUMP"),
        };
    }

    /// <summary>
    /// Finds the equip's <c>.img.xml</c> by trying each <see cref="EquipFolders"/> entry and
    /// returning the first that exists (null when none do). Result is cached, including the miss.
    /// </summary>
    private string? ResolveEquipPath(int itemId)
    {
        foreach (string folder in EquipFolders)
        {
            string path = Path.Combine(_wzRoot, "Character", folder, $"{itemId:00000000}.img.xml");
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    /// <summary>The Consume <c>.img.xml</c> file grouping an item (by <c>itemId / 10000</c>).</summary>
    public static string ConsumeImagePath(string wzRoot, int itemId)
        => Path.Combine(wzRoot, "Item", "Consume", $"{itemId / 10000:0000}.img.xml");

    /// <summary>
    /// The <c>.img.xml</c> file grouping an item, with the subfolder chosen by the item category
    /// (the leading digit of <c>itemId / 1000000</c>): 2→Consume, 3→Install, 4→Etc, 5→Cash.
    /// Equips (1, in the Character tree) and unknown categories return null.
    /// </summary>
    public static string? ItemImagePath(string wzRoot, int itemId)
    {
        string? subFolder = (itemId / 1000000) switch
        {
            2 => "Consume",
            3 => "Install",
            4 => "Etc",
            5 => "Cash",
            _ => null,
        };
        return subFolder is null
            ? null
            : Path.Combine(wzRoot, "Item", subFolder, $"{itemId / 10000:0000}.img.xml");
    }
}

/// <summary>An in-memory item provider for tests / seeded content.</summary>
public sealed class InMemoryItemProvider : IItemProvider
{
    private readonly Dictionary<int, ConsumeSpec> _items;
    private readonly Dictionary<int, int> _prices;
    private readonly Dictionary<int, EquipStats> _equips;
    private readonly Dictionary<int, double> _unitPrices;

    public InMemoryItemProvider(
        IEnumerable<ConsumeSpec> items,
        IReadOnlyDictionary<int, int>? prices = null,
        IReadOnlyDictionary<int, EquipStats>? equips = null,
        IReadOnlyDictionary<int, double>? unitPrices = null)
    {
        _items = items.ToDictionary(i => i.ItemId);
        _prices = prices is null ? new Dictionary<int, int>() : new Dictionary<int, int>(prices);
        _equips = equips is null ? new Dictionary<int, EquipStats>() : new Dictionary<int, EquipStats>(equips);
        _unitPrices = unitPrices is null ? new Dictionary<int, double>() : new Dictionary<int, double>(unitPrices);
    }

    public ConsumeSpec? GetConsume(int itemId) => _items.TryGetValue(itemId, out ConsumeSpec? s) ? s : null;

    public int? GetPrice(int itemId) => _prices.TryGetValue(itemId, out int p) ? p : null;

    public EquipStats? GetEquipStats(int itemId) => _equips.TryGetValue(itemId, out EquipStats? e) ? e : null;

    public double? GetUnitPrice(int itemId) => _unitPrices.TryGetValue(itemId, out double u) ? u : null;
}
