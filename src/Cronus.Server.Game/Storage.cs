using System.Collections.Concurrent;
using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>
/// An account's storage (the "Trunk" / 倉庫): a meso balance and a capped list of stored items,
/// shared across the account's characters (ports <c>TacosStorage</c>). Item order within a category
/// is the wire order; each stored item occupies one slot.
/// </summary>
public sealed class Storage
{
    /// <summary>Default storage capacity (slots) for a new account.</summary>
    public const int DefaultSlots = 12;

    public int Slots { get; set; } = DefaultSlots;

    public int Meso { get; set; }

    /// <summary>Stored items (their <see cref="InventoryItem.Position"/> is unused).</summary>
    public List<InventoryItem> Items { get; } = new();

    /// <summary>True when no more items fit (ports <c>TacosStorage.isFull</c>).</summary>
    public bool IsFull => Items.Count >= Slots;
}

/// <summary>
/// Account-scoped storages, created on demand (shared across all sessions) — loaded from the
/// repository when one is configured and a saved snapshot exists. <see cref="Save"/> persists an
/// account's storage after a change (no-op without a repository).
/// </summary>
public sealed class StorageRegistry
{
    private readonly IStorageRepository? _repository;
    private readonly ConcurrentDictionary<int, Storage> _byAccount = new();

    public StorageRegistry(IStorageRepository? repository = null) => _repository = repository;

    public Storage Get(int accountId) => _byAccount.GetOrAdd(accountId, Load);

    private Storage Load(int accountId)
    {
        if (_repository?.Find(accountId) is not { } saved)
        {
            return new Storage();
        }

        var storage = new Storage { Meso = saved.Meso, Slots = saved.Slots };
        storage.Items.AddRange(saved.Items);
        return storage;
    }

    /// <summary>Persists an account's storage after a deposit/withdraw/meso change.</summary>
    public void Save(int accountId)
    {
        if (_repository is not null && _byAccount.TryGetValue(accountId, out Storage? storage))
        {
            _repository.Save(accountId, new StorageData(storage.Meso, storage.Slots, storage.Items.ToList()));
        }
    }
}

/// <summary><c>LP_TrunkResult</c> operation / result codes (JMS v186 wire values, <c>OpsTrunk.init</c>).</summary>
public enum TrunkOp : byte
{
    GetSuccess = 8,
    GetFailInventoryFull = 9,
    PutSuccess = 12,
    PutIncorrectRequest = 13,
    PutNoMoney = 15,
    PutNoSpace = 16,
    MoneySuccess = 18,
    OpenTrunkDlg = 21,
}

/// <summary>The 8-byte DBCHAR field-mask bits used by <c>LP_TrunkResult</c> (<c>OpsDBCHAR</c>).</summary>
public static class TrunkMask
{
    public const long Money = 0x2;
    public const long Equip = 0x4;
    public const long Consume = 0x8;
    public const long Install = 0x10;
    public const long Etc = 0x20;
    public const long Cash = 0x40;

    /// <summary>DBCHAR_ALL — every field (used for the full open/sort dump).</summary>
    public const long All = -1;

    /// <summary>The item-category bit for an inventory tab (1 equip .. 5 cash), or 0.</summary>
    public static long CategoryBit(int tab) => tab switch
    {
        1 => Equip,
        2 => Consume,
        3 => Install,
        4 => Etc,
        5 => Cash,
        _ => 0,
    };
}
