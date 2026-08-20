using System.Collections.Concurrent;

namespace Cronus.Domain;

/// <summary>Character store port. Implemented in-memory (dev/tests) and by EF Core (MySQL).</summary>
public interface ICharacterRepository
{
    /// <summary>All characters for an account in a given world, ordered by id.</summary>
    IReadOnlyList<Character> ListByAccount(int accountId, int worldId);

    Character? Find(int characterId);

    /// <summary>True if the name is already taken (case-insensitive).</summary>
    bool NameExists(string name);

    Character Create(Character character);
}

/// <summary>Thread-safe in-memory character store for local development and tests.</summary>
public sealed class InMemoryCharacterRepository : ICharacterRepository
{
    private readonly ConcurrentDictionary<int, Character> _characters = new();
    private int _nextId;

    public IReadOnlyList<Character> ListByAccount(int accountId, int worldId)
        => _characters.Values
            .Where(c => c.AccountId == accountId && c.WorldId == worldId)
            .OrderBy(c => c.Id)
            .ToList();

    public Character? Find(int characterId)
        => _characters.TryGetValue(characterId, out Character? character) ? character : null;

    public bool NameExists(string name)
        => _characters.Values.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

    public Character Create(Character character)
    {
        character.Id = Interlocked.Increment(ref _nextId);
        _characters[character.Id] = character;
        return character;
    }
}
