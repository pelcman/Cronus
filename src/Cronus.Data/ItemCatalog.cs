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
    private static readonly Regex ItemEntry = new("<imgdir name=\"(\\d{4,8})\">", RegexOptions.Compiled);

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

    public WzItemCatalog(string wzRoot) => _categories = new(() => Load(wzRoot));

    public IReadOnlyList<ItemCategory> Categories => _categories.Value;

    private static IReadOnlyList<ItemCategory> Load(string wzRoot)
    {
        string root = Path.Combine(wzRoot, "String");
        var categories = new List<ItemCategory>();

        string eqpPath = Path.Combine(root, "Eqp.img.xml");
        if (File.Exists(eqpPath))
        {
            string xml = File.ReadAllText(eqpPath);
            foreach ((string key, string label) in EquipGroups)
            {
                IReadOnlyList<int> ids = IdsInEquipGroup(xml, key);
                if (ids.Count > 0)
                {
                    categories.Add(new ItemCategory(key, label, ids));
                }
            }
        }

        foreach ((string file, string label) in FlatTables)
        {
            string path = Path.Combine(root, $"{file}.img.xml");
            if (!File.Exists(path))
            {
                continue;
            }

            IReadOnlyList<int> ids = SortedIds(ItemEntry.Matches(File.ReadAllText(path)));
            if (ids.Count > 0)
            {
                categories.Add(new ItemCategory(file, label, ids));
            }
        }

        return categories;
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
