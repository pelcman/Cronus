using Cronus.Domain;
using Cronus.Server.Game;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>The registries load saved state from their repository and push changes back on Save.</summary>
public class GameStatePersistenceTests
{
    private sealed class FakeStorageRepo : IStorageRepository
    {
        public Dictionary<int, StorageData> Saved { get; } = new();

        public StorageData? Find(int accountId) => Saved.TryGetValue(accountId, out StorageData? d) ? d : null;

        public void Save(int accountId, StorageData data) => Saved[accountId] = data;
    }

    private sealed class FakeKeymapRepo : IKeymapRepository
    {
        public Dictionary<int, IReadOnlyDictionary<int, KeyBinding>> Saved { get; } = new();

        public IReadOnlyDictionary<int, KeyBinding>? Find(int characterId)
            => Saved.TryGetValue(characterId, out IReadOnlyDictionary<int, KeyBinding>? b) ? b : null;

        public void Save(int characterId, IReadOnlyDictionary<int, KeyBinding> bindings)
            => Saved[characterId] = bindings;
    }

    [Fact]
    public void StorageRegistry_LoadsSavedSnapshot()
    {
        var repo = new FakeStorageRepo();
        repo.Saved[42] = new StorageData(500, 8, new[] { new InventoryItem { ItemId = 2000000, Quantity = 9 } });

        Storage storage = new StorageRegistry(repo).Get(42);

        Assert.Equal(500, storage.Meso);
        Assert.Equal(8, storage.Slots);
        Assert.Equal(9, Assert.Single(storage.Items).Quantity);
    }

    [Fact]
    public void StorageRegistry_Save_PushesTheCurrentState()
    {
        var repo = new FakeStorageRepo();
        var registry = new StorageRegistry(repo);

        Storage storage = registry.Get(7);   // fresh (nothing saved yet)
        storage.Meso = 1234;
        storage.Items.Add(new InventoryItem { ItemId = 4000019, Quantity = 3 });
        registry.Save(7);

        StorageData saved = repo.Saved[7];
        Assert.Equal(1234, saved.Meso);
        Assert.Equal(3, Assert.Single(saved.Items).Quantity);
    }

    [Fact]
    public void KeymapRegistry_LoadsSavedBindings_ElseDefault()
    {
        var repo = new FakeKeymapRepo();
        repo.Saved[5] = new Dictionary<int, KeyBinding> { [20] = new KeyBinding(1, 1001003) };
        var registry = new KeymapRegistry(repo);

        Assert.Equal(new KeyBinding(1, 1001003), registry.Get(5).Get(20)); // saved layout wins
        Assert.Equal(new KeyBinding(4, 10), registry.Get(6).Get(2));       // unsaved -> default
    }

    [Fact]
    public void KeymapRegistry_Save_PushesTheCurrentBindings()
    {
        var repo = new FakeKeymapRepo();
        var registry = new KeymapRegistry(repo);

        registry.Get(9).Set(31, new KeyBinding(1, 2001002));
        registry.Save(9);

        Assert.Equal(new KeyBinding(1, 2001002), repo.Saved[9][31]);
    }
}
