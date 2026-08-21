namespace Cronus.Server.Channel;

/// <summary>How a member left a party — shapes the <c>LP_PartyResult</c> departure packet.</summary>
public enum PartyDepart
{
    /// <summary>A non-leader chose to leave.</summary>
    Leave,

    /// <summary>A member was kicked by the leader.</summary>
    Expel,

    /// <summary>The leader left, dissolving the whole party.</summary>
    Disband,
}

/// <summary>
/// A snapshot of one party slot as the client's party window wants it (ports the fields of
/// <c>MaplePartyCharacter</c> that <c>addPartyStatus</c> serialises). An empty slot is
/// <c>default</c> (id 0, blank name, offline). <paramref name="Channel"/> is 1-based like the
/// reference; the wire "member channel" is <c>Channel - 1</c> when online, -2 when not.
/// </summary>
public readonly record struct PartyMemberView(
    int Id,
    string Name,
    int Job,
    int Level,
    int MapId,
    int Channel,
    bool Online)
{
    /// <summary>Door fields default to "no Mystic Door cast" (matches <c>MaplePartyCharacter</c>).</summary>
    public const int NoDoor = 999999999;
}

/// <summary>
/// One party: up to six online members with a leader, independent of which map they're on (ports
/// <c>MapleParty</c>, minus DB persistence — Cronus parties are in-memory and online-only). Packet
/// fan-out uses each member's live session. Thread-safe: membership mutations are locked.
/// </summary>
public sealed class Party
{
    /// <summary>Maximum party size.</summary>
    public const int Capacity = 6;

    private readonly List<FieldPlayer> _members = new();
    private readonly object _gate = new();

    public Party(int id, FieldPlayer leader)
    {
        Id = id;
        LeaderId = leader.Character.Id;
        _members.Add(leader);
    }

    public int Id { get; }

    public int LeaderId { get; private set; }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _members.Count;
            }
        }
    }

    public bool IsFull => Count >= Capacity;

    public IReadOnlyList<FieldPlayer> Members
    {
        get
        {
            lock (_gate)
            {
                return _members.ToList();
            }
        }
    }

    public bool Contains(int characterId) => MemberById(characterId) is not null;

    public bool IsLeader(int characterId) => LeaderId == characterId;

    public FieldPlayer? MemberById(int characterId)
    {
        lock (_gate)
        {
            foreach (FieldPlayer m in _members)
            {
                if (m.Character.Id == characterId)
                {
                    return m;
                }
            }

            return null;
        }
    }

    /// <summary>Adds a member; false if the party is full or they're already in it.</summary>
    public bool TryAdd(FieldPlayer player)
    {
        lock (_gate)
        {
            if (_members.Count >= Capacity || _members.Any(m => m.Character.Id == player.Character.Id))
            {
                return false;
            }

            _members.Add(player);
            return true;
        }
    }

    /// <summary>Removes a member; false if they weren't in the party.</summary>
    public bool Remove(int characterId)
    {
        lock (_gate)
        {
            int i = _members.FindIndex(m => m.Character.Id == characterId);
            if (i < 0)
            {
                return false;
            }

            _members.RemoveAt(i);
            return true;
        }
    }

    /// <summary>Reassigns leadership (change-leader op); false if the target isn't a member.</summary>
    public bool SetLeader(int characterId)
    {
        lock (_gate)
        {
            if (!_members.Any(m => m.Character.Id == characterId))
            {
                return false;
            }

            LeaderId = characterId;
            return true;
        }
    }

    /// <summary>
    /// Builds the 6-slot member view (real members first, then empty padding) that the party-window
    /// encoder consumes. <paramref name="channel"/> is the 1-based channel all online members are on.
    /// </summary>
    public List<PartyMemberView> ViewSlots(int channel)
    {
        var slots = new List<PartyMemberView>(Capacity);
        foreach (FieldPlayer m in Members)
        {
            var c = m.Character;
            slots.Add(new PartyMemberView(c.Id, c.Name, c.Job, c.Level, c.MapId, channel, Online: true));
        }

        while (slots.Count < Capacity)
        {
            slots.Add(default); // empty slot: id 0, "", offline, channel 0
        }

        return slots;
    }
}

/// <summary>
/// The channel's live parties: create-on-demand, lookup by id or member, and disband (ports the
/// party role of <c>OdinWorld.Party</c>). A single shared instance ties players together across
/// fields, so it is injected like <see cref="FieldRegistry"/>.
/// </summary>
public sealed class PartyRegistry
{
    private readonly Dictionary<int, Party> _byId = new();
    private readonly Dictionary<int, Party> _byCharacter = new();
    private readonly object _gate = new();
    private int _nextId = 1;

    /// <summary>The party a character is in, or null.</summary>
    public Party? GetForCharacter(int characterId)
    {
        lock (_gate)
        {
            return _byCharacter.TryGetValue(characterId, out Party? p) ? p : null;
        }
    }

    /// <summary>Finds a party by id (for accepting an invite), or null.</summary>
    public Party? GetById(int id)
    {
        lock (_gate)
        {
            return _byId.TryGetValue(id, out Party? p) ? p : null;
        }
    }

    /// <summary>Creates a new party led by <paramref name="leader"/> and binds them to it.</summary>
    public Party Create(FieldPlayer leader)
    {
        lock (_gate)
        {
            var party = new Party(_nextId++, leader);
            _byId[party.Id] = party;
            _byCharacter[leader.Character.Id] = party;
            return party;
        }
    }

    /// <summary>Binds a character to a party after they joined it.</summary>
    public void Register(int characterId, Party party)
    {
        lock (_gate)
        {
            _byCharacter[characterId] = party;
        }
    }

    /// <summary>Unbinds a character from their party mapping (after leaving/expel).</summary>
    public void Unregister(int characterId)
    {
        lock (_gate)
        {
            _byCharacter.Remove(characterId);
        }
    }

    /// <summary>Disbands a party: drops it and unbinds every member.</summary>
    public void Disband(Party party)
    {
        lock (_gate)
        {
            _byId.Remove(party.Id);
            foreach (FieldPlayer m in party.Members)
            {
                if (_byCharacter.TryGetValue(m.Character.Id, out Party? p) && p == party)
                {
                    _byCharacter.Remove(m.Character.Id);
                }
            }
        }
    }
}
