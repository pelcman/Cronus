namespace Cronus.Domain;

/// <summary>One key binding: a category <see cref="Type"/> (0 none, 1 skill, 4/5/6 UI functions) and
/// its <see cref="Action"/> (a skill id for type 1, otherwise a client function index).</summary>
public readonly record struct KeyBinding(byte Type, int Action);

/// <summary>A snapshot of an account's storage for persistence (meso, capacity, stored items).</summary>
public sealed record StorageData(int Meso, int Slots, IReadOnlyList<InventoryItem> Items);

/// <summary>Persistence port for account storage ("Trunk") contents.</summary>
public interface IStorageRepository
{
    /// <summary>The stored snapshot for an account, or null when none was saved yet.</summary>
    StorageData? Find(int accountId);

    void Save(int accountId, StorageData data);
}

/// <summary>Persistence port for a character's function-key layout.</summary>
public interface IKeymapRepository
{
    /// <summary>The saved bindings (key index → binding), or null when none were saved yet.</summary>
    IReadOnlyDictionary<int, KeyBinding>? Find(int characterId);

    void Save(int characterId, IReadOnlyDictionary<int, KeyBinding> bindings);
}
