using Cronus.Domain;
using Xunit;

namespace Cronus.Database.Tests;

/// <summary>
/// The zero-setup SQLite persistence path: a database FILE that survives process restarts (each
/// factory reopen simulates one) and accepts the additive schema sync on reopen.
/// </summary>
public sealed class SqliteDatabaseTests
{
    [Fact]
    public void CharactersSurviveAFactoryReopen()
    {
        string dbFile = Path.Combine(Path.GetTempPath(), $"cronus-sqlite-{Guid.NewGuid()}.db");
        try
        {
            // "First boot": create the schema, save an account + character with an item.
            Func<CronusDbContext> factory = SqliteDatabase.CreateFactory(dbFile);
            SqliteDatabase.EnsureCreated(factory);

            int charId;
            {
                var accounts = new DbAccountRepository(factory);
                var characters = new DbCharacterRepository(factory);
                Account account = accounts.Create("looter", "pw", gender: 0);
                Character hero = characters.Create(new Character
                {
                    AccountId = account.Id, WorldId = 0, Name = "Persist", MapId = 100000000, Level = 12,
                });
                hero.Skills[1000] = 3;
                hero.EquippedItems.Add(new InventoryItem { ItemId = 4000000, Position = 1, Quantity = 5 });
                characters.Save(hero);
                charId = hero.Id;
            }

            // "Restart": a fresh factory over the same file — EnsureCreated must be a no-op
            // (additive sync only) and everything must come back.
            Func<CronusDbContext> reopened = SqliteDatabase.CreateFactory(dbFile);
            SqliteDatabase.EnsureCreated(reopened);

            var repo = new DbCharacterRepository(reopened);
            Character? loaded = repo.Find(charId);

            Assert.NotNull(loaded);
            Assert.Equal("Persist", loaded!.Name);
            Assert.Equal(12, loaded.Level);
            Assert.Equal(3, loaded.Skills[1000]);
            InventoryItem item = Assert.Single(loaded.EquippedItems);
            Assert.Equal(4000000, item.ItemId);
            Assert.Equal(5, item.Quantity);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(dbFile);
        }
    }

    [Fact]
    public void RemovedItems_StayRemovedAfterAReload()
    {
        string dbFile = Path.Combine(Path.GetTempPath(), $"cronus-sqlite-{Guid.NewGuid()}.db");
        try
        {
            Func<CronusDbContext> factory = SqliteDatabase.CreateFactory(dbFile);
            SqliteDatabase.EnsureCreated(factory);

            var repo = new DbCharacterRepository(factory);
            Character hero = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Consumer", MapId = 100000000 });
            hero.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 1, Quantity = 10 });
            hero.EquippedItems.Add(new InventoryItem { ItemId = 4000000, Position = 1, Quantity = 5 });
            repo.Save(hero);

            // "Consume" the potion stack: remove it from the entity and save again.
            Character loaded = repo.Find(hero.Id)!;
            loaded.EquippedItems.RemoveAll(i => i.ItemId == 2000000);
            repo.Save(loaded);

            // The row must NOT resurrect on reload (the dupe/leak bug).
            Character reloaded = repo.Find(hero.Id)!;
            InventoryItem survivor = Assert.Single(reloaded.EquippedItems);
            Assert.Equal(4000000, survivor.ItemId);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            File.Delete(dbFile);
        }
    }
}
