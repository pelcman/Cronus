using System.Text.RegularExpressions;

namespace Cronus.Data;

/// <summary>One row of a shop's stock (ports the <c>shopitems</c> schema).</summary>
public sealed record ShopItem(int ItemId, int Price, int Position, int ReqItem, int ReqItemQ);

/// <summary>An NPC shop: its id, the NPC that opens it, and its stock in display order.</summary>
public sealed class Shop
{
    public required int ShopId { get; init; }

    public required int NpcId { get; init; }

    public required IReadOnlyList<ShopItem> Items { get; init; }
}

/// <summary>Provides NPC shops by the NPC that opens them (or by shop id).</summary>
public interface IShopProvider
{
    /// <summary>The shop an NPC opens, or null when the NPC has no shop.</summary>
    Shop? GetShopByNpc(int npcId);

    /// <summary>A shop by its id, or null.</summary>
    Shop? GetShop(int shopId);
}

/// <summary>
/// A shop provider backed by the reference <c>shops</c> + <c>shopitems</c> SQL dump (as found in
/// <c>init_data_set.sql</c>). <c>shops(shopid, npcid)</c> maps an NPC to a shop; <c>shopitems
/// (shopitemid, shopid, itemid, price, position, reqitem, reqitemq)</c> holds the stock. Both tables
/// live in a larger dump alongside unrelated tables, so parsing is scoped to each table's INSERT
/// block (there may be several per table). Mirrors <c>MapleShop.createFromDB</c>.
/// </summary>
public sealed class SqlShopProvider : IShopProvider
{
    private static readonly RegexOptions Opts =
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Singleline;

    private static readonly Regex ShopItemsBlock = new(@"INSERT\s+INTO\s+`?shopitems`?.*?;", Opts);
    private static readonly Regex ShopsBlock = new(@"INSERT\s+INTO\s+`?shops`?\s*\(.*?;", Opts);

    // shopitems tuple: (shopitemid, shopid, itemid, price, position, reqitem, reqitemq).
    private static readonly Regex ItemRow = new(
        @"\(\s*-?\d+\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*,\s*(-?\d+)\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // shops tuple: (shopid, npcid).
    private static readonly Regex ShopRow = new(
        @"\(\s*(-?\d+)\s*,\s*(-?\d+)\s*\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IReadOnlyDictionary<int, Shop> _byShop;
    private readonly IReadOnlyDictionary<int, Shop> _byNpc;

    private SqlShopProvider(IReadOnlyDictionary<int, Shop> byShop, IReadOnlyDictionary<int, Shop> byNpc)
    {
        _byShop = byShop;
        _byNpc = byNpc;
    }

    public Shop? GetShopByNpc(int npcId) => _byNpc.TryGetValue(npcId, out Shop? shop) ? shop : null;

    public Shop? GetShop(int shopId) => _byShop.TryGetValue(shopId, out Shop? shop) ? shop : null;

    /// <summary>Loads shops from a SQL dump file (empty provider if missing).</summary>
    public static SqlShopProvider LoadFile(string path)
        => File.Exists(path) ? Parse(File.ReadAllText(path)) : Parse(string.Empty);

    /// <summary>Parses the <c>shops</c> + <c>shopitems</c> INSERT blocks of a SQL dump.</summary>
    public static SqlShopProvider Parse(string sqlText)
    {
        // shopid -> its stock, in display (position) order.
        var stock = new Dictionary<int, List<ShopItem>>();
        foreach (Match block in ShopItemsBlock.Matches(sqlText))
        {
            foreach (Match row in ItemRow.Matches(block.Value))
            {
                int shopId = int.Parse(row.Groups[1].ValueSpan);
                var item = new ShopItem(
                    ItemId: int.Parse(row.Groups[2].ValueSpan),
                    Price: int.Parse(row.Groups[3].ValueSpan),
                    Position: int.Parse(row.Groups[4].ValueSpan),
                    ReqItem: int.Parse(row.Groups[5].ValueSpan),
                    ReqItemQ: int.Parse(row.Groups[6].ValueSpan));

                if (!stock.TryGetValue(shopId, out List<ShopItem>? list))
                {
                    list = new List<ShopItem>();
                    stock[shopId] = list;
                }

                list.Add(item);
            }
        }

        var byShop = new Dictionary<int, Shop>();
        var byNpc = new Dictionary<int, Shop>();
        foreach (Match block in ShopsBlock.Matches(sqlText))
        {
            foreach (Match row in ShopRow.Matches(block.Value))
            {
                int shopId = int.Parse(row.Groups[1].ValueSpan);
                int npcId = int.Parse(row.Groups[2].ValueSpan);
                IReadOnlyList<ShopItem> items = stock.TryGetValue(shopId, out List<ShopItem>? list)
                    ? list.OrderBy(i => i.Position).ToList()
                    : Array.Empty<ShopItem>();

                var shop = new Shop { ShopId = shopId, NpcId = npcId, Items = items };
                byShop[shopId] = shop;
                byNpc[npcId] = shop;
            }
        }

        return new SqlShopProvider(byShop, byNpc);
    }
}

/// <summary>An in-memory shop provider for tests / seeded content.</summary>
public sealed class InMemoryShopProvider : IShopProvider
{
    private readonly Dictionary<int, Shop> _byShop;
    private readonly Dictionary<int, Shop> _byNpc;

    public InMemoryShopProvider(IEnumerable<Shop> shops)
    {
        _byShop = shops.ToDictionary(s => s.ShopId);
        _byNpc = _byShop.Values.ToDictionary(s => s.NpcId);
    }

    public Shop? GetShopByNpc(int npcId) => _byNpc.TryGetValue(npcId, out Shop? shop) ? shop : null;

    public Shop? GetShop(int shopId) => _byShop.TryGetValue(shopId, out Shop? shop) ? shop : null;
}
