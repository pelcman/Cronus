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

    /// <summary>Maximum stack size for this item (default 100).</summary>
    public int SlotMax { get; init; } = 100;
}

/// <summary>Provides item metadata (currently consumable specs) by item id.</summary>
public interface IItemProvider
{
    ConsumeSpec? GetConsume(int itemId);

    /// <summary>The item's wz <c>info/price</c>, or null when unknown / not found.</summary>
    int? GetPrice(int itemId);
}

/// <summary>
/// Loads consumable specs from a wz_xml tree: <c>Item/Consume/{itemId/10000:0000}.img.xml</c>,
/// then the <c>{itemId:00000000}</c> node's <c>spec</c> / <c>info</c> (cached). Missing → null.
/// </summary>
public sealed class WzItemProvider : IItemProvider
{
    private readonly string _wzRoot;
    private readonly ConcurrentDictionary<int, ConsumeSpec?> _cache = new();
    private readonly ConcurrentDictionary<int, int?> _priceCache = new();

    public WzItemProvider(string wzRoot) => _wzRoot = wzRoot;

    public ConsumeSpec? GetConsume(int itemId) => _cache.GetOrAdd(itemId, Load);

    public int? GetPrice(int itemId) => _priceCache.GetOrAdd(itemId, LoadPrice);

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

    public InMemoryItemProvider(IEnumerable<ConsumeSpec> items, IReadOnlyDictionary<int, int>? prices = null)
    {
        _items = items.ToDictionary(i => i.ItemId);
        _prices = prices is null ? new Dictionary<int, int>() : new Dictionary<int, int>(prices);
    }

    public ConsumeSpec? GetConsume(int itemId) => _items.TryGetValue(itemId, out ConsumeSpec? s) ? s : null;

    public int? GetPrice(int itemId) => _prices.TryGetValue(itemId, out int p) ? p : null;
}
