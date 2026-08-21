using System.Linq.Expressions;
using System.Text.Json;
using Cronus.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cronus.Database;

/// <summary>
/// EF Core context for Cronus persistence. Currently maps accounts; character and world
/// tables follow as those models land. Kept provider-agnostic — the concrete provider
/// (Pomelo/MySQL for production, InMemory for tests) is chosen when building the options.
/// </summary>
public sealed class CronusDbContext : DbContext
{
    public CronusDbContext(DbContextOptions<CronusDbContext> options)
        : base(options)
    {
    }

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<Character> Characters => Set<Character>();

    public DbSet<InventoryItem> Items => Set<InventoryItem>();

    public DbSet<StorageEntity> Storages => Set<StorageEntity>();

    public DbSet<KeymapEntity> Keymaps => Set<KeymapEntity>();

    public DbSet<GuildData> Guilds => Set<GuildData>();

    public DbSet<HiredMerchantEntity> HiredMerchants => Set<HiredMerchantEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var account = modelBuilder.Entity<Account>();
        account.ToTable("accounts");
        account.HasKey(a => a.Id);
        account.Property(a => a.Id).ValueGeneratedOnAdd();
        account.Property(a => a.LoginId).HasMaxLength(13).IsRequired();
        account.HasIndex(a => a.LoginId).IsUnique();
        account.Property(a => a.Password).HasMaxLength(128).IsRequired();
        account.Property(a => a.Gender);
        account.Property(a => a.IsGameMaster);

        var character = modelBuilder.Entity<Character>();
        character.ToTable("characters");
        character.HasKey(c => c.Id);
        character.Property(c => c.Id).ValueGeneratedOnAdd();
        character.Property(c => c.Name).HasMaxLength(13).IsRequired();
        character.HasIndex(c => c.Name).IsUnique();
        character.HasIndex(c => new { c.AccountId, c.WorldId });
        character.HasMany(c => c.EquippedItems)
            .WithOne()
            .HasForeignKey(i => i.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
        // Skills and quest state persist as JSON columns on the character row (small,
        // character-scoped maps — simpler than side tables and enough for these dictionaries).
        MapJsonDictionary(character, c => c.Skills);          // skillId -> level
        MapJsonDictionary(character, c => c.StartedQuests);   // questId -> progress string
        MapJsonDictionary(character, c => c.CompletedQuests); // questId -> completion time
        MapJsonDictionary(character, c => c.Buddies);         // friendId -> buddy entry
        MapJsonDictionary(character, c => c.SkillMacros);     // slot -> skill macro

        var item = modelBuilder.Entity<InventoryItem>();
        item.ToTable("items");
        item.HasKey(i => i.Id);
        item.Property(i => i.Id).ValueGeneratedOnAdd();
        item.HasIndex(i => i.CharacterId);
        item.Property(i => i.Owner).HasMaxLength(13);

        // Account storage and per-character key layouts: one row each, contents as JSON
        // (the same small-map pattern as the character's JSON columns above).
        var storage = modelBuilder.Entity<StorageEntity>();
        storage.ToTable("storages");
        storage.HasKey(s => s.AccountId);
        storage.Property(s => s.AccountId).ValueGeneratedNever();

        var keymap = modelBuilder.Entity<KeymapEntity>();
        keymap.ToTable("keymaps");
        keymap.HasKey(k => k.CharacterId);
        keymap.Property(k => k.CharacterId).ValueGeneratedNever();

        var merchant = modelBuilder.Entity<HiredMerchantEntity>();
        merchant.ToTable("hiredmerch");
        merchant.HasKey(m => m.OwnerId);
        merchant.Property(m => m.OwnerId).ValueGeneratedNever();
        merchant.Property(m => m.OwnerName).HasMaxLength(13);

        var guild = modelBuilder.Entity<GuildData>();
        guild.ToTable("guilds");
        guild.HasKey(g => g.Id);
        guild.Property(g => g.Id).ValueGeneratedOnAdd();
        guild.Property(g => g.Name).HasMaxLength(45).IsRequired();
        guild.HasIndex(g => g.Name).IsUnique();
        guild.Property(g => g.Notice).HasMaxLength(101);
        // The five rank titles as one JSON text column.
        guild.Property(g => g.RankTitles).HasConversion(
            titles => JsonSerializer.Serialize(titles, JsonOptions),
            json => JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>(),
            new ValueComparer<List<string>>(
                (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                titles => JsonSerializer.Serialize(titles, JsonOptions).GetHashCode(),
                titles => JsonSerializer.Deserialize<List<string>>(
                    JsonSerializer.Serialize(titles, JsonOptions), JsonOptions) ?? new List<string>()));
    }

    private static readonly JsonSerializerOptions JsonOptions = new();

    /// <summary>
    /// Maps an <c>int</c>-keyed dictionary property to a single JSON text column, with a value
    /// comparer so EF Core detects in-place mutations (skill-ups, quest updates) on SaveChanges.
    /// </summary>
    private static void MapJsonDictionary<TValue>(
        EntityTypeBuilder<Character> entity,
        Expression<Func<Character, Dictionary<int, TValue>>> property)
    {
        entity.Property(property).HasConversion(
            map => JsonSerializer.Serialize(map, JsonOptions),
            json => JsonSerializer.Deserialize<Dictionary<int, TValue>>(json, JsonOptions) ?? new Dictionary<int, TValue>(),
            new ValueComparer<Dictionary<int, TValue>>(
                (a, b) => JsonSerializer.Serialize(a, JsonOptions) == JsonSerializer.Serialize(b, JsonOptions),
                map => JsonSerializer.Serialize(map, JsonOptions).GetHashCode(),
                map => JsonSerializer.Deserialize<Dictionary<int, TValue>>(
                    JsonSerializer.Serialize(map, JsonOptions), JsonOptions) ?? new Dictionary<int, TValue>()));
    }
}
