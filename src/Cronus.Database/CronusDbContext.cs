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
        character.Ignore(c => c.EquippedItems); // item persistence is a follow-up
    }
}
