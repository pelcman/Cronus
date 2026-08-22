using System.Text.RegularExpressions;

namespace Cronus.Data;

/// <summary>Resolves NPC display names (from String.wz).</summary>
public interface INpcNameProvider
{
    /// <summary>The NPC's display name, or null when unknown.</summary>
    string? GetName(int npcId);
}

/// <summary>
/// Loads NPC names from <c>String/Npc.img.xml</c> once, lazily. The file is one large flat list of
/// <c>&lt;imgdir name="{id}"&gt;&lt;string name="name" value="…"/&gt;</c> entries, so a single regex
/// sweep is faster and lighter than a full DOM parse.
/// </summary>
public sealed class WzNpcNameProvider : INpcNameProvider
{
    private static readonly Regex EntryPattern = new(
        "<imgdir name=\"(\\d+)\">\\s*<string name=\"name\" value=\"([^\"]*)\"",
        RegexOptions.Compiled);

    private readonly Lazy<IReadOnlyDictionary<int, string>> _names;

    public WzNpcNameProvider(string wzRoot)
    {
        _names = new Lazy<IReadOnlyDictionary<int, string>>(() => Load(wzRoot));
    }

    public string? GetName(int npcId)
        => _names.Value.TryGetValue(npcId, out string? name) ? name : null;

    private static IReadOnlyDictionary<int, string> Load(string wzRoot)
    {
        string path = Path.Combine(wzRoot, "String", "Npc.img.xml");
        if (!File.Exists(path))
        {
            return new Dictionary<int, string>();
        }

        var names = new Dictionary<int, string>();
        string xml = File.ReadAllText(path);
        foreach (Match m in EntryPattern.Matches(xml))
        {
            if (int.TryParse(m.Groups[1].Value, out int id) && m.Groups[2].Value.Length > 0)
            {
                names[id] = System.Net.WebUtility.HtmlDecode(m.Groups[2].Value);
            }
        }

        return names;
    }
}
