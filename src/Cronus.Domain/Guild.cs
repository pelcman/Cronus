using System.Collections.Concurrent;

namespace Cronus.Domain;

/// <summary>
/// A guild's persistent state (ports the <c>guilds</c> table / <c>MapleGuild</c>'s core fields).
/// Membership itself lives on each <see cref="Character"/> (<see cref="Character.GuildId"/> /
/// <see cref="Character.GuildRank"/>), so member lists are derived, never duplicated here.
/// </summary>
public sealed class GuildData
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int LeaderId { get; set; }

    /// <summary>Rank titles 1 (master) … 5 (lowest member); the reference DB defaults.</summary>
    public List<string> RankTitles { get; set; } = new() { "Master", "Jr. Master", "Member", "Member", "Member" };

    public int Capacity { get; set; } = 10;

    // Emblem (the "guild mark"): background/foreground graphic + color indices.
    public short LogoBG { get; set; }
    public byte LogoBGColor { get; set; }
    public short Logo { get; set; }
    public byte LogoColor { get; set; }

    public string Notice { get; set; } = string.Empty;

    /// <summary>Guild points.</summary>
    public int Gp { get; set; }

    /// <summary>Emblem-change counter encoded per member row (0 for a fresh guild).</summary>
    public int Signature { get; set; }
}

/// <summary>Guild store port. Implemented in-memory (dev/tests) and by EF Core (MySQL).</summary>
public interface IGuildRepository
{
    GuildData? Find(int guildId);

    /// <summary>A guild by name (case-insensitive), or null.</summary>
    GuildData? FindByName(string name);

    /// <summary>All guilds (for startup diagnostics / rankings).</summary>
    IReadOnlyList<GuildData> ListAll();

    GuildData Create(GuildData guild);

    void Save(GuildData guild);

    bool Delete(int guildId);
}

/// <summary>Thread-safe in-memory guild store for local development and tests.</summary>
public sealed class InMemoryGuildRepository : IGuildRepository
{
    private readonly ConcurrentDictionary<int, GuildData> _guilds = new();
    private int _nextId;

    public GuildData? Find(int guildId)
        => _guilds.TryGetValue(guildId, out GuildData? guild) ? guild : null;

    public GuildData? FindByName(string name)
        => _guilds.Values.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<GuildData> ListAll() => _guilds.Values.OrderBy(g => g.Id).ToList();

    public GuildData Create(GuildData guild)
    {
        guild.Id = Interlocked.Increment(ref _nextId);
        _guilds[guild.Id] = guild;
        return guild;
    }

    // The stored instance is the same reference callers mutate, so there is nothing to flush.
    public void Save(GuildData guild) => _guilds[guild.Id] = guild;

    public bool Delete(int guildId) => _guilds.TryRemove(guildId, out _);
}
