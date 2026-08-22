using Cronus.Domain;
using Microsoft.EntityFrameworkCore;

namespace Cronus.Database;

/// <summary>
/// EF Core-backed <see cref="ICharacterRepository"/>. Context-per-operation via the supplied
/// factory (same pattern as <see cref="DbAccountRepository"/>), provider-agnostic.
/// </summary>
public sealed class DbCharacterRepository : ICharacterRepository
{
    private readonly Func<CronusDbContext> _contextFactory;

    public DbCharacterRepository(Func<CronusDbContext> contextFactory)
        => _contextFactory = contextFactory;

    public IReadOnlyList<Character> ListByAccount(int accountId, int worldId)
    {
        using CronusDbContext db = _contextFactory();
        return db.Characters
            .Include(c => c.EquippedItems)
            .Where(c => c.AccountId == accountId && c.WorldId == worldId)
            .OrderBy(c => c.Id)
            .ToList();
    }

    public Character? Find(int characterId)
    {
        using CronusDbContext db = _contextFactory();
        return db.Characters
            .Include(c => c.EquippedItems)
            .FirstOrDefault(c => c.Id == characterId);
    }

    public bool NameExists(string name)
    {
        using CronusDbContext db = _contextFactory();
        string lowered = name.ToLowerInvariant();
        return db.Characters.Any(c => c.Name.ToLower() == lowered);
    }

    public Character? FindByName(string name)
    {
        using CronusDbContext db = _contextFactory();
        string lowered = name.ToLowerInvariant();
        return db.Characters
            .Include(c => c.EquippedItems)
            .FirstOrDefault(c => c.Name.ToLower() == lowered);
    }

    public IReadOnlyList<Character> ListByGuild(int guildId)
    {
        using CronusDbContext db = _contextFactory();
        return db.Characters
            .Include(c => c.EquippedItems)
            .Where(c => c.GuildId == guildId)
            .OrderBy(c => c.Id)
            .ToList();
    }

    public Character Create(Character character)
    {
        using CronusDbContext db = _contextFactory();
        db.Characters.Add(character);
        db.SaveChanges();
        return character;
    }

    public void Save(Character character)
    {
        using CronusDbContext db = _contextFactory();

        // Update() upserts the character and every item still on the entity, but it cannot know
        // about rows the entity NO LONGER holds — a consumed potion or dropped equip would
        // resurrect on the next load. Reconcile: delete any stored item row whose id is absent
        // from the in-memory list.
        var keep = character.EquippedItems.Select(i => i.Id).Where(id => id != 0).ToHashSet();
        List<int> stale = db.Items
            .Where(i => i.CharacterId == character.Id)
            .Select(i => i.Id)
            .ToList()
            .Where(id => !keep.Contains(id))
            .ToList();

        db.Characters.Update(character);
        foreach (int id in stale)
        {
            db.Entry(new InventoryItem { Id = id, ItemId = 0, CharacterId = character.Id }).State = EntityState.Deleted;
        }

        db.SaveChanges();
    }

    public bool Delete(int characterId)
    {
        using CronusDbContext db = _contextFactory();
        Character? character = db.Characters.FirstOrDefault(c => c.Id == characterId);
        if (character is null)
        {
            return false;
        }

        db.Characters.Remove(character);
        db.SaveChanges();
        return true;
    }
}
