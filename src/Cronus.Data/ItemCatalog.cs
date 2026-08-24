using System.Text.RegularExpressions;

namespace Cronus.Data;

/// <summary>One browsable group of item ids (a String.wz category), in ascending id order.</summary>
public sealed record ItemCategory(string Key, string DisplayName, IReadOnlyList<int> ItemIds);

/// <summary>Enumerates every item the game data knows, grouped into browsable categories.</summary>
public interface IItemCatalog
{
    /// <summary>The non-empty categories, in menu order.</summary>
    IReadOnlyList<ItemCategory> Categories { get; }
}

/// <summary>
/// Builds the catalog from the <c>String.wz</c> name tables — the same lists the client shows, so
/// every id here is a real, named item. Equips come from <c>Eqp.img.xml</c> split by its own
/// sub-categories (Cap / Weapon / …, minus Face and Hair, which are looks rather than items);
/// the other tabs come from <c>Consume</c>, <c>Ins</c>, <c>Etc</c>, <c>Cash</c>, and <c>Pet</c>.
/// Parsed once, lazily.
/// </summary>
public sealed class WzItemCatalog : IItemCatalog
{
    /// <summary>A top-level <c>&lt;imgdir name="Name"&gt;</c> group inside Eqp.img.xml.</summary>
    private static readonly Regex EqpGroup = new("<imgdir name=\"([A-Za-z]+)\">", RegexOptions.Compiled);

    /// <summary>An item entry: a numeric imgdir. Ids are 4-8 digits across the tables.</summary>
    private static readonly Regex ItemEntry = new("<imgdir name=\"(\\d{4,8})\"\\s*/?>", RegexOptions.Compiled);

    /// <summary>A bundle item inside an <c>Item/{tab}/{prefix}.img.xml</c> file (always 8 digits;
    /// the tag is self-closing when the entry carries no children).</summary>
    private static readonly Regex BundleEntry = new("<imgdir name=\"(\\d{8})\"\\s*/?>", RegexOptions.Compiled);

    /// <summary>Equip sub-categories in menu order, with their Japanese labels. Face and Hair are
    /// deliberately absent: they are avatar looks (see /beauty), not inventory items.</summary>
    private static readonly (string Key, string Label)[] EquipGroups =
    {
        ("Cap", "帽子"),
        ("Coat", "上衣"),
        ("Longcoat", "オーバーオール"),
        ("Pants", "ズボン"),
        ("Shoes", "靴"),
        ("Glove", "手袋"),
        ("Cape", "マント"),
        ("Shield", "盾"),
        ("Weapon", "武器"),
        ("Accessory", "アクセサリー"),
        ("Ring", "指輪"),
        ("PetEquip", "ペット装備"),
        ("Taming", "騎乗ペット"),
        ("Dragon", "ドラゴン装備"),
    };

    /// <summary>The flat (non-equip) tables: file name → label.</summary>
    private static readonly (string File, string Label)[] FlatTables =
    {
        ("Consume", "消費"),
        ("Ins", "設置"),
        ("Etc", "その他"),
        ("Cash", "キャッシュ"),
        ("Pet", "ペット"),
    };

    private readonly Lazy<IReadOnlyList<ItemCategory>> _categories;

    public WzItemCatalog(string wzRoot) : this(new DirectoryWzStore(wzRoot))
    {
    }

    public WzItemCatalog(IWzStore store) => _categories = new(() => Load(store));

    public IReadOnlyList<ItemCategory> Categories => _categories.Value;

    private static IReadOnlyList<ItemCategory> Load(IWzStore store)
    {
        var categories = new List<ItemCategory>();

        // String.wz names MANY ids the item data doesn't actually contain (unreleased/removed
        // content — 56 equips and ~89 bundles in the v186 tree). The client crashes trying to
        // render one, so a name alone is not enough: an id must also have real data.
        HashSet<int> real = RealItemIds(store);

        if (store.ReadText("String/Eqp.img.xml") is { } eqpXml)
        {
            foreach ((string key, string label) in EquipGroups)
            {
                var ids = IdsInEquipGroup(eqpXml, key).Where(real.Contains).ToList();
                if (ids.Count > 0)
                {
                    categories.Add(new ItemCategory(key, label, ids));
                }
            }
        }

        foreach ((string file, string label) in FlatTables)
        {
            if (store.ReadText($"String/{file}.img.xml") is not { } xml)
            {
                continue;
            }

            var ids = SortedIds(ItemEntry.Matches(xml)).Where(real.Contains).ToList();
            if (ids.Count > 0)
            {
                categories.Add(new ItemCategory(file, label, ids));
            }
        }

        return categories;
    }

    /// <summary>
    /// Every item id the game data actually defines — the client has the same files, so an id in
    /// here is one it can render. Equips and pets are one file each
    /// (<c>Character/**/{id:00000000}.img.xml</c>, <c>Item/Pet/{id}.img.xml</c>); bundle items are
    /// grouped by id prefix inside <c>Item/{tab}/{prefix}.img.xml</c>.
    /// </summary>
    private static HashSet<int> RealItemIds(IWzStore store)
    {
        var ids = new HashSet<int>();

        foreach (string prefix in new[] { "Character", "Item/Pet" })
        {
            foreach (string path in store.EnumeratePaths(prefix))
            {
                string name = path[(path.LastIndexOf('/') + 1)..];
                int dot = name.IndexOf('.');
                if (dot > 0 && int.TryParse(name.AsSpan(0, dot), out int id))
                {
                    ids.Add(id);
                }
            }
        }

        foreach (string path in store.EnumeratePaths("Item"))
        {
            if (path.StartsWith("Item/Pet/", StringComparison.Ordinal))
            {
                continue; // already covered by the per-file pass above
            }

            foreach (Match m in BundleEntry.Matches(store.ReadText(path) ?? ""))
            {
                if (int.TryParse(m.Groups[1].ValueSpan, out int id))
                {
                    ids.Add(id);
                }
            }
        }

        return ids;
    }

    /// <summary>The item ids inside one Eqp sub-category (up to the next sub-category header).</summary>
    private static IReadOnlyList<int> IdsInEquipGroup(string xml, string group)
    {
        Match start = EqpGroup.Matches(xml).FirstOrDefault(m => m.Groups[1].Value == group);
        if (start is null)
        {
            return Array.Empty<int>();
        }

        int from = start.Index + start.Length;
        Match next = EqpGroup.Match(xml, from);
        string span = next.Success ? xml[from..next.Index] : xml[from..];
        return SortedIds(ItemEntry.Matches(span));
    }

    private static IReadOnlyList<int> SortedIds(MatchCollection matches)
    {
        var ids = new SortedSet<int>();
        foreach (Match m in matches)
        {
            if (int.TryParse(m.Groups[1].ValueSpan, out int id) && id > 0)
            {
                ids.Add(id);
            }
        }

        return ids.ToList();
    }
}

/// <summary>An in-memory catalog for tests / seeded content.</summary>
public sealed class InMemoryItemCatalog : IItemCatalog
{
    public InMemoryItemCatalog(IEnumerable<ItemCategory> categories) => Categories = categories.ToList();

    public IReadOnlyList<ItemCategory> Categories { get; }
}
