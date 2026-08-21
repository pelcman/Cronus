using Cronus.Domain;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class AutoSaveTests
{
    /// <summary>An in-memory repo that also records which character ids were saved.</summary>
    private sealed class RecordingRepository : ICharacterRepository
    {
        private readonly InMemoryCharacterRepository _inner = new();

        public List<int> SavedIds { get; } = new();

        public IReadOnlyList<Character> ListByAccount(int accountId, int worldId) => _inner.ListByAccount(accountId, worldId);
        public Character? Find(int characterId) => _inner.Find(characterId);
        public bool NameExists(string name) => _inner.NameExists(name);
        public Character Create(Character character) => _inner.Create(character);
        public bool Delete(int characterId) => _inner.Delete(characterId);

        public void Save(Character character)
        {
            _inner.Save(character);
            SavedIds.Add(character.Id);
        }
    }

    [Fact]
    public void Tick_PersistsEveryOnlineCharacter()
    {
        var repo = new RecordingRepository();
        Character alice = repo.Create(new Character { AccountId = 1, WorldId = 0, Name = "Alice", MapId = 100000000 });
        Character bob = repo.Create(new Character { AccountId = 2, WorldId = 0, Name = "Bob", MapId = 100000000 });

        var fields = new FieldRegistry();
        Field field = fields.Get(100000000);
        field.Enter(new FieldPlayer(alice, session: null!));
        field.Enter(new FieldPlayer(bob, session: null!));

        var service = new CharacterAutoSaveService(fields, repo);
        int saved = service.Tick();

        Assert.Equal(2, saved);
        Assert.Contains(alice.Id, repo.SavedIds);
        Assert.Contains(bob.Id, repo.SavedIds);
    }

    [Fact]
    public void Tick_NoPlayers_SavesNothing()
    {
        var repo = new RecordingRepository();
        var fields = new FieldRegistry();

        var service = new CharacterAutoSaveService(fields, repo);

        Assert.Equal(0, service.Tick());
        Assert.Empty(repo.SavedIds);
    }
}
