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

/// <summary>One row of the <c>hiredmerch</c> table: a merchant's snapshot (stock/sales as JSON).</summary>
public sealed class HiredMerchantEntity
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

    public string ListingsJson { get; set; } = "[]";

    public string SalesJson { get; set; } = "[]";
}

/// <summary>EF Core-backed <see cref="IHiredMerchantRepository"/> (one row per owner).</summary>
public sealed class DbHiredMerchantRepository : IHiredMerchantRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly Func<CronusDbContext> _contextFactory;

    public DbHiredMerchantRepository(Func<CronusDbContext> contextFactory) => _contextFactory = contextFactory;

    public IReadOnlyList<HiredMerchantData> LoadAll()
    {
        using CronusDbContext db = _contextFactory();
        return db.HiredMerchants.ToList().Select(e => new HiredMerchantData
        {
            OwnerId = e.OwnerId,
            OwnerName = e.OwnerName,
            Description = e.Description,
            ItemId = e.ItemId,
            MapId = e.MapId,
            X = e.X,
            Y = e.Y,
            Foothold = e.Foothold,
            Meso = e.Meso,
            Listings = JsonSerializer.Deserialize<List<MerchantListing>>(e.ListingsJson, JsonOptions) ?? new List<MerchantListing>(),
            Sales = JsonSerializer.Deserialize<List<MerchantSale>>(e.SalesJson, JsonOptions) ?? new List<MerchantSale>(),
        }).ToList();
    }

    public void Save(HiredMerchantData merchant)
    {
        using CronusDbContext db = _contextFactory();
        HiredMerchantEntity? entity = db.HiredMerchants.Find(merchant.OwnerId);
        if (entity is null)
        {
            entity = new HiredMerchantEntity { OwnerId = merchant.OwnerId };
            db.HiredMerchants.Add(entity);
        }

        entity.OwnerName = merchant.OwnerName;
        entity.Description = merchant.Description;
        entity.ItemId = merchant.ItemId;
        entity.MapId = merchant.MapId;
        entity.X = merchant.X;
        entity.Y = merchant.Y;
        entity.Foothold = merchant.Foothold;
        entity.Meso = merchant.Meso;
        entity.ListingsJson = JsonSerializer.Serialize(merchant.Listings, JsonOptions);
        entity.SalesJson = JsonSerializer.Serialize(merchant.Sales, JsonOptions);
        db.SaveChanges();
    }

    public void Delete(int ownerId)
    {
        using CronusDbContext db = _contextFactory();
        HiredMerchantEntity? entity = db.HiredMerchants.Find(ownerId);
        if (entity is not null)
        {
            db.HiredMerchants.Remove(entity);
            db.SaveChanges();
        }
    }
}

/// <summary>EF Core-backed <see cref="IGuildRepository"/> (guild core state; membership lives on characters).</summary>
public sealed class DbGuildRepository : IGuildRepository
{
    private readonly Func<CronusDbContext> _contextFactory;

    public DbGuildRepository(Func<CronusDbContext> contextFactory) => _contextFactory = contextFactory;

    public GuildData? Find(int guildId)
    {
        using CronusDbContext db = _contextFactory();
        return db.Guilds.Find(guildId);
    }

    public GuildData? FindByName(string name)
    {
        using CronusDbContext db = _contextFactory();
        string lowered = name.ToLowerInvariant();
        return db.Guilds.FirstOrDefault(g => g.Name.ToLower() == lowered);
    }

    public IReadOnlyList<GuildData> ListAll()
    {
        using CronusDbContext db = _contextFactory();
        return db.Guilds.OrderBy(g => g.Id).ToList();
    }

    public GuildData Create(GuildData guild)
    {
        using CronusDbContext db = _contextFactory();
        db.Guilds.Add(guild);
        db.SaveChanges();
        return guild;
    }

    public void Save(GuildData guild)
    {
        using CronusDbContext db = _contextFactory();
        db.Guilds.Update(guild);
        db.SaveChanges();
    }

    public bool Delete(int guildId)
    {
        using CronusDbContext db = _contextFactory();
        GuildData? guild = db.Guilds.Find(guildId);
        if (guild is null)
        {
            return false;
        }

        db.Guilds.Remove(guild);
        db.SaveChanges();
        return true;
    }
}
