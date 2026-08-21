using System.Collections.Concurrent;
using Jint;
using Jint.Runtime;

namespace Cronus.Scripting;

/// <summary>Provides portal script source by the portal's script name.</summary>
public interface IPortalScriptSource
{
    /// <summary>JavaScript source for a named portal script, or null if there is none.</summary>
    string? GetScript(string name);
}

/// <summary>In-memory portal script source for tests / seeded portals.</summary>
public sealed class DictionaryPortalScriptSource : IPortalScriptSource
{
    private readonly Dictionary<string, string> _scripts;

    public DictionaryPortalScriptSource(IDictionary<string, string> scripts)
        => _scripts = new Dictionary<string, string>(scripts, StringComparer.Ordinal);

    public string? GetScript(string name) => _scripts.TryGetValue(name, out string? code) ? code : null;
}

/// <summary>
/// Loads portal scripts from a folder of <c>{name}.js</c> files (cached). Matches the upstream
/// convention of one script file per portal script name.
/// </summary>
public sealed class FolderPortalScriptSource : IPortalScriptSource
{
    private readonly string _root;
    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.Ordinal);

    public FolderPortalScriptSource(string root) => _root = root;

    public string? GetScript(string name) => _cache.GetOrAdd(name, Load);

    private string? Load(string name)
    {
        // Guard against path traversal from a wz-provided name; use the bare file name only.
        string safe = Path.GetFileName(name);
        string path = Path.Combine(_root, $"{safe}.js");
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }
}

/// <summary>
/// Runs portal scripts with Jint. Unlike NPC scripts, a portal script does not carry a blocking
/// dialog — it typically just checks a condition and warps — so it runs to completion in one shot
/// with only the <c>player</c> global (ports the role of <c>PortalScriptManager</c>).
/// </summary>
public sealed class PortalScriptEngine
{
    private readonly IPortalScriptSource _scripts;

    public PortalScriptEngine(IPortalScriptSource scripts) => _scripts = scripts;

    /// <summary>Runs the named portal script with <paramref name="player"/>; a no-op if none exists.</summary>
    public void Run(string scriptName, object player)
    {
        string? code = _scripts.GetScript(scriptName);
        if (code is null)
        {
            return;
        }

        try
        {
            var engine = new Engine(options => options.LimitRecursion(64));
            engine.SetValue("player", player);
            engine.Execute(code);
            engine.Invoke("start");
        }
        catch (JavaScriptException)
        {
            // Script bug: ignore rather than break the portal.
        }
        catch (Exception)
        {
            // Any other failure (including a warp error) shouldn't crash the handler.
        }
    }
}
