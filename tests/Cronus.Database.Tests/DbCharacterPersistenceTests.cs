using Cronus.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cronus.Database.Tests;

/// <summary>
/// Skill / quest persistence (the JSON-column mapping) exercised over a real relational provider.
/// SQLite (in-memory) applies EF Core value converters — the InMemory provider does not — so this
/// actually round-trips the JSON, unlike the account tests.
/// </summary>
public sealed class DbCharacterPersistenceTests
{
    // One open connection = one in-memory database shared across the short-lived contexts.
    private static (Func<CronusDbContext> Factory, SqliteConnection Connection) SqliteFactory()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        DbContextOptions<CronusDbContext> options =
            new DbContextOptionsBuilder<CronusDbContext>()
                .UseSqlite(connection)
                .Options;
        using (var db = new CronusDbContext(options))
        {
            db.Database.EnsureCreated();
        }

        return (() => new CronusDbContext(options), connection);
    }

    [Fact]
    public void SkillsAndQuests_SurviveReload()
    {
        (Func<CronusDbContext> factory, SqliteConnection connection) = SqliteFactory();
        try
        {
            var repo = new DbCharacterRepository(factory);
            Character hero = repo.Create(new Character
            {
                AccountId = 1,
                WorldId = 0,
                Name = "Skiller",
                MapId = 100000000,
            });

            hero.Skills[1000] = 3;
            hero.Skills[1001] = 1;
            hero.StartedQuests[2000] = "progress-10";
            hero.CompletedQuests[2001] = 1_700_000_000L;
            repo.Save(hero);

            // Reload in a fresh context — proves the maps came back from the JSON columns.
            Character? reloaded = repo.Find(hero.Id);

            Assert.NotNull(reloaded);
            Assert.Equal(3, reloaded!.Skills[1000]);
            Assert.Equal(1, reloaded.Skills[1001]);
            Assert.Equal("progress-10", reloaded.StartedQuests[2000]);
            Assert.Equal(1_700_000_000L, reloaded.CompletedQuests[2001]);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public void SkillUp_OnReloadedCharacter_PersistsNewLevel()
    {
        (Func<CronusDbContext> factory, SqliteConnection connection) = SqliteFactory();
        try
        {
            var repo = new DbCharacterRepository(factory);
            Character hero = repo.Create(new Character
            {
                AccountId = 1,
                WorldId = 0,
                Name = "Mage",
                MapId = 100000000,
            });
            hero.Skills[2000000] = 1;
            repo.Save(hero);

            // Mutate a freshly loaded character (as the skill-up handler does) and save again.
            Character loaded = repo.Find(hero.Id)!;
            loaded.Skills[2000000] += 1;
            repo.Save(loaded);

            Assert.Equal(2, repo.Find(hero.Id)!.Skills[2000000]);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public void NoSkillsOrQuests_ReloadsAsEmptyMaps()
    {
        (Func<CronusDbContext> factory, SqliteConnection connection) = SqliteFactory();
        try
        {
            var repo = new DbCharacterRepository(factory);
            Character hero = repo.Create(new Character
            {
                AccountId = 1,
                WorldId = 0,
                Name = "Blank",
                MapId = 100000000,
            });

            Character? reloaded = repo.Find(hero.Id);

            Assert.NotNull(reloaded);
            Assert.Empty(reloaded!.Skills);
            Assert.Empty(reloaded.StartedQuests);
            Assert.Empty(reloaded.CompletedQuests);
        }
        finally
        {
            connection.Dispose();
        }
    }
}
