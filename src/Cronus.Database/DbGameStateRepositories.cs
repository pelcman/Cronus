using System.Text.Json;
using Cronus.Domain;

namespace Cronus.Database;

/// <summary>One row of the <c>storages</c> table: an account's storage snapshot (items as JSON).</summary>
public sealed class StorageEntity
{
    public int AccountId { get; set; }

    public int Meso { get; set; }

    public int Slots { get; set; }

    public string ItemsJson { get; set; } = "[]";
}

/// <summary>One row of the <c>keymaps</c> table: a character's key layout (bindings as JSON).</summary>
public sealed class KeymapEntity
{
    public int CharacterId { get; set; }

    public string BindingsJson { get; set; } = "{}";
}

/// <summary>
/// EF Core-backed <see cref="IStorageRepository"/>. Context-per-operation via the supplied factory
/// (same pattern as <see cref="DbCharacterRepository"/>); the item list rides as a JSON column.
/// </summary>
public sealed class DbStorageRepository : IStorageRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly Func<CronusDbContext> _contextFactory;

    public DbStorageRepository(Func<CronusDbContext> contextFactory) => _contextFactory = contextFactory;

    public StorageData? Find(int accountId)
    {
        using CronusDbContext db = _contextFactory();
        StorageEntity? entity = db.Storages.Find(accountId);
        if (entity is null)
        {
            return null;
        }

        List<InventoryItem> items =
            JsonSerializer.Deserialize<List<InventoryItem>>(entity.ItemsJson, JsonOptions) ?? new List<InventoryItem>();
        return new StorageData(entity.Meso, entity.Slots, items);
    }

    public void Save(int accountId, StorageData data)
    {
        using CronusDbContext db = _contextFactory();
        StorageEntity? entity = db.Storages.Find(accountId);
        if (entity is null)
        {
            entity = new StorageEntity { AccountId = accountId };
            db.Storages.Add(entity);
        }

        entity.Meso = data.Meso;
        entity.Slots = data.Slots;
        entity.ItemsJson = JsonSerializer.Serialize(data.Items, JsonOptions);
        db.SaveChanges();
    }
}

/// <summary>EF Core-backed <see cref="IKeymapRepository"/> (bindings as one JSON column per character).</summary>
public sealed class DbKeymapRepository : IKeymapRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly Func<CronusDbContext> _contextFactory;

    public DbKeymapRepository(Func<CronusDbContext> contextFactory) => _contextFactory = contextFactory;

    public IReadOnlyDictionary<int, KeyBinding>? Find(int characterId)
    {
        using CronusDbContext db = _contextFactory();
        KeymapEntity? entity = db.Keymaps.Find(characterId);
        if (entity is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<int, KeyBinding>>(entity.BindingsJson, JsonOptions)
            ?? new Dictionary<int, KeyBinding>();
    }

    public void Save(int characterId, IReadOnlyDictionary<int, KeyBinding> bindings)
    {
        using CronusDbContext db = _contextFactory();
        KeymapEntity? entity = db.Keymaps.Find(characterId);
        if (entity is null)
        {
            entity = new KeymapEntity { CharacterId = characterId };
            db.Keymaps.Add(entity);
        }

        entity.BindingsJson = JsonSerializer.Serialize(bindings, JsonOptions);
        db.SaveChanges();
    }
}
