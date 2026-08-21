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

/// <summary>One listing in a persisted hired merchant (item template + remaining bundles + price).</summary>
public sealed record MerchantListing(InventoryItem Item, short Bundles, int Price);

/// <summary>One recorded sale in a persisted hired merchant.</summary>
public sealed record MerchantSale(int ItemId, short Quantity, int TotalPrice, string Buyer);

/// <summary>A hired merchant's persistent state (one per owner), keyed by the owner character.</summary>
public sealed class HiredMerchantData
{
    public int OwnerId { get; set; }

    public string OwnerName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int ItemId { get; set; }

    public int MapId { get; set; }

    public short X { get; set; }

    public short Y { get; set; }

    public int Foothold { get; set; }

    public int Meso { get; set; }

    public List<MerchantListing> Listings { get; set; } = new();

    public List<MerchantSale> Sales { get; set; } = new();
}

/// <summary>Persistence port for hired merchants (so open stores survive a server restart).</summary>
public interface IHiredMerchantRepository
{
    IReadOnlyList<HiredMerchantData> LoadAll();

    void Save(HiredMerchantData merchant);

    void Delete(int ownerId);
}
