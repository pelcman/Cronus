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

    [Fact]
    public void Guild_RoundTrips_TitlesEmblemAndNotice()
    {
        var repo = new DbGuildRepository(InMemoryFactory());

        GuildData created = repo.Create(new GuildData
        {
            Name = "Cronus",
            LeaderId = 7,
            Notice = "Welcome!",
            LogoBG = 1001,
            LogoBGColor = 2,
            Logo = 4005,
            LogoColor = 3,
        });
        Assert.True(created.Id > 0);

        created.RankTitles = new List<string> { "王", "副官", "兵", "兵", "新入り" };
        repo.Save(created);

        GuildData? loaded = repo.Find(created.Id);
        Assert.NotNull(loaded);
        Assert.Equal("Cronus", loaded!.Name);
        Assert.Equal(7, loaded.LeaderId);
        Assert.Equal("Welcome!", loaded.Notice);
        Assert.Equal(1001, loaded.LogoBG);
        Assert.Equal(4005, loaded.Logo);
        Assert.Equal(new[] { "王", "副官", "兵", "兵", "新入り" }, loaded.RankTitles);

        Assert.NotNull(repo.FindByName("cronus")); // case-insensitive
        Assert.True(repo.Delete(created.Id));
        Assert.Null(repo.Find(created.Id));
    }
}
