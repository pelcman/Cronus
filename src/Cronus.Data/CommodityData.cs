using System.Text.RegularExpressions;

namespace Cronus.Data;

/// <summary>One cash-shop catalog entry (Etc.wz <c>Commodity.img</c>).</summary>
public sealed record Commodity(int Sn, int ItemId, short Count, int Price);

/// <summary>Resolves cash-shop commodity serials to their item/price.</summary>
public interface ICommodityProvider
{
    Commodity? GetBySn(int sn);
}

/// <summary>
/// Loads the commodity catalog from <c>Etc/Commodity.img.xml</c> once, lazily. The file is a
/// flat list of <c>&lt;imgdir&gt;</c> entries with SN/ItemId/Count/Price ints, so a regex sweep
/// (the same approach as <see cref="WzNpcNameProvider"/>) keeps it fast.
/// </summary>
public sealed class WzCommodityProvider : ICommodityProvider
{
    private static readonly Regex FieldPattern = new(
        "<int name=\"(SN|ItemId|Count|Price)\" value=\"(-?\\d+)\"", RegexOptions.Compiled);

    private readonly Lazy<IReadOnlyDictionary<int, Commodity>> _bySn;

    public WzCommodityProvider(string wzRoot)
    {
        _bySn = new(() => Load(wzRoot));
    }

    public Commodity? GetBySn(int sn)
        => _bySn.Value.TryGetValue(sn, out Commodity? c) ? c : null;

    private static IReadOnlyDictionary<int, Commodity> Load(string wzRoot)
    {
        string path = Path.Combine(wzRoot, "Etc", "Commodity.img.xml");
        var map = new Dictionary<int, Commodity>();
        if (!File.Exists(path))
        {
            return map;
        }

        string xml = File.ReadAllText(path);
        foreach (string block in xml.Split("<imgdir name=\"", StringSplitOptions.RemoveEmptyEntries))
        {
            int sn = 0, itemId = 0, count = 1, price = 0;
            foreach (Match m in FieldPattern.Matches(block))
            {
                int value = int.Parse(m.Groups[2].Value);
                switch (m.Groups[1].Value)
                {
                    case "SN": sn = value; break;
                    case "ItemId": itemId = value; break;
                    case "Count": count = value; break;
                    case "Price": price = value; break;
                }
            }

            if (sn > 0 && itemId > 0)
            {
                map[sn] = new Commodity(sn, itemId, (short)Math.Clamp(count, 1, short.MaxValue), price);
            }
        }

        return map;
    }
}
