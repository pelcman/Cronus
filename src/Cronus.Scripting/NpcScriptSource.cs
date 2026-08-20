using System.Collections.Concurrent;

namespace Cronus.Scripting;

/// <summary>Provides NPC script source by NPC id.</summary>
public interface INpcScriptSource
{
    /// <summary>JavaScript source for an NPC, or null if the NPC has no script.</summary>
    string? GetScript(int npcId);
}

/// <summary>In-memory script source for tests / seeded NPCs.</summary>
public sealed class DictionaryNpcScriptSource : INpcScriptSource
{
    private readonly Dictionary<int, string> _scripts;

    public DictionaryNpcScriptSource(IDictionary<int, string> scripts)
        => _scripts = new Dictionary<int, string>(scripts);

    public string? GetScript(int npcId) => _scripts.TryGetValue(npcId, out string? code) ? code : null;
}

/// <summary>
/// Loads NPC scripts from a folder of <c>{npcId}.js</c> files (cached). Matches the upstream
/// convention of one script file per NPC id.
/// </summary>
public sealed class FolderNpcScriptSource : INpcScriptSource
{
    private readonly string _root;
    private readonly ConcurrentDictionary<int, string?> _cache = new();

    public FolderNpcScriptSource(string root) => _root = root;

    public string? GetScript(int npcId) => _cache.GetOrAdd(npcId, Load);

    private string? Load(int npcId)
    {
        string path = Path.Combine(_root, $"{npcId}.js");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}
