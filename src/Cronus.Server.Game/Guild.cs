using System.Collections.Concurrent;
using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>
/// Channel-wide guild coordinator (ports the guild half of <c>OdinWorld.Guild</c> +
/// <c>GuildHandler</c>'s invite list): resolves guilds through the repository, tracks which
/// members are online for broadcasts, and holds pending invitations (invitee name → guild id).
/// </summary>
public sealed class GuildRegistry
{
    private readonly IGuildRepository _repo;
    private readonly ConcurrentDictionary<int, ConcurrentDictionary<int, FieldPlayer>> _online = new();
    private readonly ConcurrentDictionary<string, int> _invites = new(StringComparer.OrdinalIgnoreCase);

    public GuildRegistry(IGuildRepository? repo = null) => _repo = repo ?? new InMemoryGuildRepository();

    public GuildData? Get(int guildId) => guildId > 0 ? _repo.Find(guildId) : null;

    public GuildData? FindByName(string name) => _repo.FindByName(name);

    /// <summary>Creates a guild with the reference defaults and the creator as leader (rank 1).</summary>
    public GuildData Create(string name, int leaderId)
        => _repo.Create(new GuildData { Name = name, LeaderId = leaderId });

    public void Save(GuildData guild) => _repo.Save(guild);

    public bool Delete(int guildId)
    {
        _online.TryRemove(guildId, out _);
        return _repo.Delete(guildId);
    }

    /// <summary>Marks a member's session online for guild broadcasts.</summary>
    public void SetOnline(int guildId, FieldPlayer player)
        => _online.GetOrAdd(guildId, _ => new ConcurrentDictionary<int, FieldPlayer>())[player.Character.Id] = player;

    public void SetOffline(int guildId, int characterId)
    {
        if (_online.TryGetValue(guildId, out ConcurrentDictionary<int, FieldPlayer>? members))
        {
            members.TryRemove(characterId, out _);
        }
    }

    /// <summary>The guild's online members (for broadcasts).</summary>
    public IReadOnlyCollection<FieldPlayer> OnlineMembers(int guildId)
        => _online.TryGetValue(guildId, out ConcurrentDictionary<int, FieldPlayer>? members)
            ? members.Values.ToArray()
            : Array.Empty<FieldPlayer>();

    /// <summary>Records a pending invitation for a player (by name) into a guild.</summary>
    public void Invite(string inviteeName, int guildId) => _invites[inviteeName] = guildId;

    /// <summary>Consumes a pending invitation; true when one matched the guild.</summary>
    public bool TakeInvite(string inviteeName, int guildId)
        => _invites.TryGetValue(inviteeName, out int gid) && gid == guildId && _invites.TryRemove(inviteeName, out _);
}
