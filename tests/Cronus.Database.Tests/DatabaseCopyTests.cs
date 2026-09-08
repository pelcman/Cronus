using Cronus.Database;
using Cronus.Domain;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cronus.Database.Tests;

/// <summary>
/// The legacy-save import: every table copies across with its keys intact, so a character keeps
/// its id, its account link, its items and its JSON columns after moving stores.
/// </summary>
public class DatabaseCopyTests
{
    private static (Func<CronusDbContext> Factory, SqliteConnection Connection) Memory()
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
    public void CopyAll_MovesEveryTableWithKeysIntact()
    {
        (Func<CronusDbContext> source, SqliteConnection sourceConn) = Memory();
        (Func<CronusDbContext> target, SqliteConnection targetConn) = Memory();
        try
        {
            int accountId;
            int heroId;
            using (CronusDbContext db = source())
            {
                var account = new Account { LoginId = "alpha", Password = "pw" };
                db.Accounts.Add(account);
                db.SaveChanges();
                accountId = account.Id;

                var hero = new Character { AccountId = accountId, WorldId = 0, Name = "Copied", MapId = 100000000 };
                hero.Skills[1000] = 3;
                db.Characters.Add(hero);
                db.SaveChanges();
                heroId = hero.Id;

                db.Items.Add(new InventoryItem { CharacterId = heroId, ItemId = 1302000, Position = -11 });
                db.Storages.Add(new StorageEntity { AccountId = accountId, Meso = 5, Slots = 4 });
                db.Keymaps.Add(new KeymapEntity { CharacterId = heroId, BindingsJson = "{\"1\":[1,2]}" });
                db.Parcels.Add(new ParcelEntity { ToCharacterId = heroId, FromName = "Doi", Meso = 7, SentAt = 1 });
                db.SaveChanges();
            }

            DatabaseCopy.Report report = DatabaseCopy.CopyAll(source, target);

            Assert.Equal(1, report.Accounts);
            Assert.Equal(1, report.Characters);
            Assert.Equal(1, report.Items);
            Assert.Equal(1, report.Storages);
            Assert.Equal(1, report.Keymaps);
            Assert.Equal(1, report.Parcels);
            Assert.Equal(6, report.Total);

            using CronusDbContext copy = target();
            Character copied = copy.Characters.Single();
            Assert.Equal(heroId, copied.Id);
            Assert.Equal(accountId, copied.AccountId);
            Assert.Equal("Copied", copied.Name);
            Assert.Equal(3, copied.Skills[1000]);
            Assert.Equal(heroId, copy.Items.Single().CharacterId);
            Assert.Equal(accountId, copy.Storages.Single().AccountId);
            Assert.Equal("{\"1\":[1,2]}", copy.Keymaps.Single().BindingsJson);
            Assert.Equal(heroId, copy.Parcels.Single().ToCharacterId);
            Assert.Equal("alpha", copy.Accounts.Single().LoginId);
        }
        finally
        {
            sourceConn.Dispose();
            targetConn.Dispose();
        }
    }

    [Fact]
    public void CopyAll_OnAnEmptySource_CopiesNothing()
    {
        (Func<CronusDbContext> source, SqliteConnection sourceConn) = Memory();
        (Func<CronusDbContext> target, SqliteConnection targetConn) = Memory();
        try
        {
            Assert.Equal(0, DatabaseCopy.CopyAll(source, target).Total);
        }
        finally
        {
            sourceConn.Dispose();
            targetConn.Dispose();
        }
    }
}
