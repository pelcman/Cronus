using Cronus.Database;
using Cronus.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cronus.Database.Tests;

/// <summary>
/// Round-trips for the storage and keymap repositories over the EF in-memory provider (repository
/// logic + entity mapping; MySQL-specific behavior still needs a live server).
/// </summary>
public class DbGameStateRepositoryTests
{
    private static Func<CronusDbContext> InMemoryFactory()
    {
        string dbName = Guid.NewGuid().ToString();
        DbContextOptions<CronusDbContext> options =
            new DbContextOptionsBuilder<CronusDbContext>()
                .UseInMemoryDatabase(dbName)
                .Options;
        return () => new CronusDbContext(options);
    }

    [Fact]
    public void Storage_RoundTrips_MesoSlotsAndItems()
    {
        var repo = new DbStorageRepository(InMemoryFactory());

        var sword = new InventoryItem { ItemId = 1302000, Quantity = 1, Watk = 17, UpgradeSlots = 7 };
        var potions = new InventoryItem { ItemId = 2000000, Quantity = 30 };
        repo.Save(42, new StorageData(Meso: 12345, Slots: 12, Items: new[] { sword, potions }));

        StorageData? loaded = repo.Find(42);

        Assert.NotNull(loaded);
        Assert.Equal(12345, loaded!.Meso);
        Assert.Equal(12, loaded.Slots);
        Assert.Equal(2, loaded.Items.Count);
        InventoryItem loadedSword = Assert.Single(loaded.Items, i => i.ItemId == 1302000);
        Assert.Equal(17, loadedSword.Watk);            // equip stats survive the JSON round-trip
        Assert.Equal(7, loadedSword.UpgradeSlots);
        Assert.Equal(30, Assert.Single(loaded.Items, i => i.ItemId == 2000000).Quantity);
    }

    [Fact]
    public void Storage_SaveTwice_Overwrites()
    {
        var repo = new DbStorageRepository(InMemoryFactory());
        repo.Save(7, new StorageData(100, 4, Array.Empty<InventoryItem>()));
        repo.Save(7, new StorageData(999, 8, new[] { new InventoryItem { ItemId = 4000019, Quantity = 5 } }));

        StorageData? loaded = repo.Find(7);
        Assert.Equal(999, loaded!.Meso);
        Assert.Equal(8, loaded.Slots);
        Assert.Single(loaded.Items);
    }

    [Fact]
    public void Storage_Find_Unknown_IsNull()
    {
        Assert.Null(new DbStorageRepository(InMemoryFactory()).Find(999));
    }

    [Fact]
    public void Keymap_RoundTrips_Bindings()
    {
        var repo = new DbKeymapRepository(InMemoryFactory());

        repo.Save(5, new Dictionary<int, KeyBinding>
        {
            [2] = new KeyBinding(4, 10),
            [20] = new KeyBinding(1, 1001003), // a skill binding
        });

        IReadOnlyDictionary<int, KeyBinding>? loaded = repo.Find(5);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Count);
        Assert.Equal(new KeyBinding(4, 10), loaded[2]);
        Assert.Equal(new KeyBinding(1, 1001003), loaded[20]);
    }

    [Fact]
    public void Keymap_Find_Unknown_IsNull()
    {
        Assert.Null(new DbKeymapRepository(InMemoryFactory()).Find(999));
    }
}
