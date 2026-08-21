using System.Collections.Concurrent;
using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>
/// A character's function-key map (ports <c>TacosKeyLayout</c>): key index → binding. JMS v186 uses a
/// fixed 94-slot layout on the wire (unbound slots are 0/0); a new character starts from the
/// reference's built-in default layout.
/// </summary>
public sealed class Keymap
{
    /// <summary>The number of key slots on the wire for JMS v186.</summary>
    public const int KeyCount = 94;

    private readonly Dictionary<int, KeyBinding> _bindings;

    private Keymap(Dictionary<int, KeyBinding> bindings) => _bindings = bindings;

    /// <summary>The binding at a key index, or null when unbound.</summary>
    public KeyBinding? Get(int key) => _bindings.TryGetValue(key, out KeyBinding b) ? b : null;

    /// <summary>Binds a key (type != 0); ports <c>changeKeybinding</c>.</summary>
    public void Set(int key, KeyBinding binding) => _bindings[key] = binding;

    /// <summary>Clears a key (a change with type 0).</summary>
    public void Remove(int key) => _bindings.Remove(key);

    // The reference's default key layout for a new character (DQ_KeyMap.add): three parallel arrays.
    private static readonly int[] DefaultKeys =
    {
        2, 3, 4, 5, 6, 7, 16, 17, 18, 19, 23, 25, 31, 34, 37, 38, 44, 45, 46, 50, 59, 60,
        61, 62, 63, 64, 65, 8, 9, 24, 30, 10, 11, 12, 20, 33, 35, 39, 40, 47, 48, 49,
    };

    private static readonly byte[] DefaultTypes =
    {
        4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 4, 5, 5, 5, 4, 6, 6,
        6, 6, 6, 6, 6, 4, 4, 4, 5, 4, 4, 4, 4, 4, 4, 4, 4, 5, 4, 4,
    };

    private static readonly int[] DefaultActions =
    {
        10, 12, 13, 18, 23, 28, 8, 5, 0, 4, 1, 9, 2, 17, 3, 20, 50, 52, 53, 7, 100, 101,
        102, 103, 104, 105, 106, 19, 14, 24, 51, 15, 16, 22, 27, 25, 11, 26, 16, 54, 21, 6,
    };

    /// <summary>Builds a fresh keymap seeded with the reference's default bindings.</summary>
    public static Keymap CreateDefault()
    {
        var bindings = new Dictionary<int, KeyBinding>(DefaultKeys.Length);
        for (int i = 0; i < DefaultKeys.Length; i++)
        {
            bindings[DefaultKeys[i]] = new KeyBinding(DefaultTypes[i], DefaultActions[i]);
        }

        return new Keymap(bindings);
    }

    /// <summary>Rebuilds a keymap from persisted bindings.</summary>
    public static Keymap FromBindings(IReadOnlyDictionary<int, KeyBinding> bindings)
        => new(new Dictionary<int, KeyBinding>(bindings));

    /// <summary>A copy of the current bindings, for persistence.</summary>
    public IReadOnlyDictionary<int, KeyBinding> Snapshot() => new Dictionary<int, KeyBinding>(_bindings);
}

/// <summary>
/// Per-character key layouts, created on demand — from the repository when one is configured and a
/// saved layout exists, else from the default. <see cref="Save"/> persists a character's layout
/// (no-op without a repository).
/// </summary>
public sealed class KeymapRegistry
{
    private readonly IKeymapRepository? _repository;
    private readonly ConcurrentDictionary<int, Keymap> _byCharacter = new();

    public KeymapRegistry(IKeymapRepository? repository = null) => _repository = repository;

    public Keymap Get(int characterId) => _byCharacter.GetOrAdd(characterId, Load);

    private Keymap Load(int characterId)
        => _repository?.Find(characterId) is { } saved ? Keymap.FromBindings(saved) : Keymap.CreateDefault();

    /// <summary>Persists a character's layout after a rebind.</summary>
    public void Save(int characterId)
    {
        if (_repository is not null && _byCharacter.TryGetValue(characterId, out Keymap? keymap))
        {
            _repository.Save(characterId, keymap.Snapshot());
        }
    }
}
