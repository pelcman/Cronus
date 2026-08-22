using Cronus.Common;
using Cronus.Domain;
using Cronus.Network.Packets;
using Cronus.Server.Game;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cronus.Database.Tests;

/// <summary>
/// The re-login crash surface: a character with picked-up items is saved to the DB and reloaded in
/// a fresh context, then its full entry blob (<c>WriteAllData</c>) is compared to the pre-save blob.
/// The bot suite runs on in-memory accounts and never exercises this DB round-trip, so a field that
/// the persistence layer drops or changes (which would malform the entry blob and crash the client
/// with "error code 38 / end of file" on re-login) can only be caught here.
/// </summary>
public sealed class DbInventoryReloadTests
{
    private static (Func<CronusDbContext> Factory, SqliteConnection Connection) SqliteFactory()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        DbContextOptions<CronusDbContext> options =
            new DbContextOptionsBuilder<CronusDbContext>().UseSqlite(connection).Options;
        using (var db = new CronusDbContext(options))
        {
            db.Database.EnsureCreated();
        }

        return (() => new CronusDbContext(options), connection);
    }

    private static byte[] EntryBlob(Character c)
    {
        var w = new PacketWriter(encoding: ServerConfig.Jms186.CodePage);
        CharacterDataEncoder.WriteAllData(w, c);
        return w.ToArray();
    }

    [Fact]
    public void PickedUpItems_SurviveDbReload_EntryBlobUnchanged()
    {
        (Func<CronusDbContext> factory, SqliteConnection connection) = SqliteFactory();
        try
        {
            var repo = new DbCharacterRepository(factory);
            Character hero = repo.Create(new Character
            {
                AccountId = 1,
                WorldId = 0,
                Name = "Looter",
                MapId = 100000000,
                Level = 50,
                Job = 112,
            });

            // Skills (one 4th-job, one 1st-job) and a quest, so the whole blob is exercised.
            hero.Skills[1120004] = 30;   // 4th job (master level)
            hero.Skills[1000] = 3;       // 1st job
            hero.StartedQuests[2000] = "progress";

            // Picked-up items across every encode path.
            hero.EquippedItems.Add(new InventoryItem { ItemId = 1302000, Position = 1, Quantity = 1, UpgradeSlots = 7, Watk = 17, Str = 5, Durability = 12000 }); // equip w/ stats
            hero.EquippedItems.Add(new InventoryItem { ItemId = 2000000, Position = 1, Quantity = 200 }); // USE bundle
            hero.EquippedItems.Add(new InventoryItem { ItemId = 4000000, Position = 1, Quantity = 50 });  // ETC
            hero.EquippedItems.Add(new InventoryItem { ItemId = 2070006, Position = 2, Quantity = 800 }); // throwing stars (207 tail)
            hero.EquippedItems.Add(new InventoryItem { ItemId = 1112300, Position = 2, Quantity = 1, CashId = 0x0102030405060708 }); // cash ring
            hero.EquippedItems.Add(new InventoryItem { ItemId = 5000000, Position = 3, Quantity = 1, PetName = "ペット", PetLevel = 5, PetCloseness = 100, PetFullness = 80 }); // pet
            repo.Save(hero);

            byte[] before = EntryBlob(hero);

            Character reloaded = repo.Find(hero.Id)!;
            byte[] after = EntryBlob(reloaded);

            Assert.Equal(before.Length, after.Length);
            Assert.Equal(before, after);
        }
        finally
        {
            connection.Dispose();
        }
    }
}
