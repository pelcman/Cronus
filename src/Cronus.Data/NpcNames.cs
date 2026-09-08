using System.Text.RegularExpressions;

namespace Cronus.Data;

/// <summary>Resolves NPC display names (from String.wz).</summary>
public interface INpcNameProvider
{
    /// <summary>The NPC's display name, or null when unknown.</summary>
    string? GetName(int npcId);

    /// <summary>
    /// True when the client has the NPC's image (<c>Npc.wz/{id:D7}.img</c>). A dialog for an NPC
    /// without one crashes the client with STG_E_FILENOTFOUND (0x80030002) — found by /sweep npcs
    /// on 2026-09-09 (2151003, a GMS-only instructor id that JMS names but does not draw).
    /// </summary>
    bool HasImage(int npcId);
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

    public WzNpcNameProvider(string wzRoot) : this(new DirectoryWzStore(wzRoot))
    {
    }

    public WzNpcNameProvider(IWzStore store)
    {
        _store = store;
        _names = new Lazy<IReadOnlyDictionary<int, string>>(() => Load(store));
    }

    private readonly IWzStore _store;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<int, bool> _images = new();

    public bool HasImage(int npcId) => _images.GetOrAdd(npcId, id => _store.Exists($"Npc/{id:D7}.img.xml"));

    public string? GetName(int npcId)
        => _names.Value.TryGetValue(npcId, out string? name) ? name : null;

    private static IReadOnlyDictionary<int, string> Load(IWzStore store)
    {
        string? xml = store.ReadText("String/Npc.img.xml");
        if (xml is null)
        {
            return new Dictionary<int, string>();
        }

        var names = new Dictionary<int, string>();
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
