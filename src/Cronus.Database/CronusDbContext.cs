using Cronus.Domain;
using Microsoft.EntityFrameworkCore;

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
        character.Ignore(c => c.StartedQuests);   // quest persistence is a follow-up
        character.Ignore(c => c.CompletedQuests);
        character.Ignore(c => c.Skills);          // skill persistence is a follow-up

        var item = modelBuilder.Entity<InventoryItem>();
        item.ToTable("items");
        item.HasKey(i => i.Id);
        item.Property(i => i.Id).ValueGeneratedOnAdd();
        item.HasIndex(i => i.CharacterId);
        item.Property(i => i.Owner).HasMaxLength(13);
    }
}
