using Cronus.Database;
using Cronus.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cronus.Database.Tests;

/// <summary>
/// The save path must survive a row that vanished between saves (the autosave tick and the session
/// racing on one character): the in-memory item is the truth and comes back, and the caller never
/// sees the concurrency exception that used to drop the session.
/// </summary>
public class DbCharacterSaveRecoveryTests
{
    private static (Func<CronusDbContext> Factory, SqliteConnection Connection) SqliteFactory()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        DbContextOptions<CronusDbContext> options =
            new DbContextOptionsBuilder<CronusDbContext>().UseSqlite(connection).Options;
        using (var db = new CronusDbContext(options))
        {
            db.Database.EnsureCreated();
        }

        return (() => new CronusDbContext(options), connection);
    }

    [Fact]
    public void Save_WhenAnItemRowWasDeletedUnderneath_ReinsertsItInsteadOfThrowing()
    {
        (Func<CronusDbContext> factory, SqliteConnection connection) = SqliteFactory();
        try
        {
            var repo = new DbCharacterRepository(factory);
            Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Racer", MapId = 100000000 });
            hero.EquippedItems.Add(new InventoryItem { CharacterId = hero.Id, ItemId = 1302000, Position = -11, Quantity = 1 });
            repo.Save(hero);
            int itemRowId = hero.EquippedItems.Single().Id;
            Assert.NotEqual(0, itemRowId);

            // Another context deletes the row (what a racing save did on 2026-09-07).
            using (CronusDbContext other = factory())
            {
                other.Items.Remove(other.Items.Single(i => i.Id == itemRowId));
                other.SaveChanges();
            }

            // The character still holds the item; saving used to throw DbUpdateConcurrencyException.
            hero.Meso += 10;
            repo.Save(hero);

            Character reloaded = repo.Find(hero.Id)!;
            Assert.Equal(1302000, Assert.Single(reloaded.EquippedItems).ItemId);
            Assert.Equal(hero.Meso, reloaded.Meso);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public void Save_FromTwoThreadsOnOneCharacter_NeverThrows()
    {
        (Func<CronusDbContext> factory, SqliteConnection connection) = SqliteFactory();
        try
        {
            var repo = new DbCharacterRepository(factory);
            Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Twins", MapId = 100000000 });
            for (int i = 0; i < 5; i++)
            {
                hero.EquippedItems.Add(new InventoryItem { CharacterId = hero.Id, ItemId = 2000000 + i, Position = (short)(i + 1), Quantity = 1 });
            }

            repo.Save(hero);

            // Both "threads" mutate the item list between saves, as the session (consuming
            // potions) and the autosave tick do; with the per-character lock nothing overlaps.
            var errors = new List<Exception>();
            Task saver = Task.Run(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        lock (hero)
                        {
                            if (hero.EquippedItems.Count > 1)
                            {
                                hero.EquippedItems.RemoveAt(0);
                            }
                        }

                        repo.Save(hero);
                    }
                    catch (Exception ex)
                    {
                        lock (errors) errors.Add(ex);
                    }
                }
            });
            Task autosave = Task.Run(() =>
            {
                for (int i = 0; i < 20; i++)
                {
                    try
                    {
                        repo.Save(hero);
                    }
                    catch (Exception ex)
                    {
                        lock (errors) errors.Add(ex);
                    }
                }
            });
            Task.WaitAll(saver, autosave);

            Assert.Empty(errors);
            Assert.Equal(hero.EquippedItems.Count, repo.Find(hero.Id)!.EquippedItems.Count);
        }
        finally
        {
            connection.Dispose();
        }
    }
}
