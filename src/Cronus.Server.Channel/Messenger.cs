using Cronus.Network;

namespace Cronus.Server.Channel;

/// <summary>
/// One messenger window: up to three players who share a private chat, independent of which map
/// they're on (ports <c>tacos.server.TacosMessenger</c>). Slots are fixed indices (0..2) so the
/// client can place each member; a leaving member frees their slot. Packet fan-out reuses each
/// member's live session. Thread-safe: slot mutations are locked, sends run on a snapshot.
/// </summary>
public sealed class Messenger
{
    /// <summary>The 3-person cap of the messenger window.</summary>
    public const int Capacity = 3;

    private readonly (FieldPlayer Player, int Channel)?[] _slots = new (FieldPlayer, int)?[Capacity];
    private readonly ChannelPackets _packets;
    private readonly object _gate = new();

    public Messenger(int id, ChannelPackets packets)
    {
        Id = id;
        _packets = packets;
    }

    /// <summary>The messenger id the client uses to join (via an invite).</summary>
    public int Id { get; }

    public bool IsEmpty
    {
        get
        {
            lock (_gate)
            {
                return Array.TrueForAll(_slots, s => s is null);
            }
        }
    }

    public bool Contains(int characterId) => IndexOf(characterId) >= 0;

    /// <summary>The slot (0..2) a member occupies, or -1 if they're not in this messenger.</summary>
    public int IndexOf(int characterId)
    {
        lock (_gate)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] is { } s && s.Player.Character.Id == characterId)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// Adds a player and announces the join: the newcomer learns their own slot and the existing
    /// members, and the existing members learn the newcomer. Returns false when the window is full.
    /// </summary>
    public async ValueTask<bool> EnterAsync(FieldPlayer player, int channel)
    {
        int index = Add(player, channel);
        if (index < 0)
        {
            return false; // full
        }

        await SendAsync(player, _packets.MessengerSelfEnterResult(index)).ConfigureAwait(false);

        foreach ((int i, FieldPlayer p, int ch) in Snapshot())
        {
            if (p.Character.Id == player.Character.Id)
            {
                continue;
            }

            // Show the newcomer the existing member, and the existing member the newcomer.
            await SendAsync(player, _packets.MessengerEnter(i, p.Character, ch, isNew: false)).ConfigureAwait(false);
            await SendAsync(p, _packets.MessengerEnter(index, player.Character, channel, isNew: true)).ConfigureAwait(false);
        }

        return true;
    }

    /// <summary>Removes a member and tells the remaining members which slot opened up.</summary>
    public async ValueTask LeaveAsync(int characterId)
    {
        (int Index, FieldPlayer Player, int Channel)? removed = RemoveInternal(characterId);
        if (removed is null)
        {
            return;
        }

        foreach ((int _, FieldPlayer p, int _) in Snapshot())
        {
            await SendAsync(p, _packets.MessengerLeave(removed.Value.Index)).ConfigureAwait(false);
        }
    }

    /// <summary>Relays a chat line to the other members (the sender sees their own echo locally).</summary>
    public async ValueTask ChatAsync(int senderCharacterId, string message)
    {
        foreach ((int _, FieldPlayer p, int _) in Snapshot())
        {
            if (p.Character.Id == senderCharacterId)
            {
                continue;
            }

            await SendAsync(p, _packets.MessengerChat(message)).ConfigureAwait(false);
        }
    }

    /// <summary>Tells every current member whether an invite reached its target.</summary>
    public async ValueTask BroadcastInviteResultAsync(string inviteeName, bool found)
    {
        foreach ((int _, FieldPlayer p, int _) in Snapshot())
        {
            await SendAsync(p, _packets.MessengerInviteResult(inviteeName, found)).ConfigureAwait(false);
        }
    }

    private int Add(FieldPlayer player, int channel)
    {
        lock (_gate)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] is null)
                {
                    _slots[i] = (player, channel);
                    return i;
                }
            }

            return -1;
        }
    }

    private (int Index, FieldPlayer Player, int Channel)? RemoveInternal(int characterId)
    {
        lock (_gate)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] is { } s && s.Player.Character.Id == characterId)
                {
                    _slots[i] = null;
                    return (i, s.Player, s.Channel);
                }
            }

            return null;
        }
    }

    private List<(int Index, FieldPlayer Player, int Channel)> Snapshot()
    {
        lock (_gate)
        {
            var list = new List<(int, FieldPlayer, int)>(Capacity);
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] is { } s)
                {
                    list.Add((i, s.Player, s.Channel));
                }
            }

            return list;
        }
    }

    private static async ValueTask SendAsync(FieldPlayer player, byte[] packet)
    {
        try
        {
            await player.Session.SendAsync(packet).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // A dead session drops out on its own disconnect path; keep fanning out.
        }
    }
}

/// <summary>
/// The channel's live messengers: create-on-demand and lookup by id or by member (ports the
/// messenger role of <c>TacosWorld</c>). A single shared instance ties players across fields
/// together, so it is injected like <see cref="FieldRegistry"/>.
/// </summary>
public sealed class MessengerRegistry
{
    private const int FirstMessengerId = 7777; // matches the reference's starting id

    private readonly Dictionary<int, Messenger> _byId = new();
    private readonly Dictionary<int, Messenger> _byCharacter = new();
    private readonly ChannelPackets _packets;
    private readonly object _gate = new();
    private int _nextId = FirstMessengerId;

    public MessengerRegistry(ChannelPackets packets) => _packets = packets;

    /// <summary>The messenger a character is currently in, or null.</summary>
    public Messenger? GetFor(int characterId)
    {
        lock (_gate)
        {
            return _byCharacter.TryGetValue(characterId, out Messenger? m) ? m : null;
        }
    }

    /// <summary>Finds a messenger by id (for accepting an invite), or null.</summary>
    public Messenger? FindById(int id)
    {
        lock (_gate)
        {
            return _byId.TryGetValue(id, out Messenger? m) ? m : null;
        }
    }

    /// <summary>Creates a fresh, empty messenger and registers it by id.</summary>
    public Messenger Create()
    {
        lock (_gate)
        {
            var m = new Messenger(_nextId++, _packets);
            _byId[m.Id] = m;
            return m;
        }
    }

    /// <summary>Binds a character to a messenger after they successfully entered it.</summary>
    public void Register(int characterId, Messenger messenger)
    {
        lock (_gate)
        {
            _byCharacter[characterId] = messenger;
        }
    }

    /// <summary>Unbinds a character and discards the messenger if that left it empty.</summary>
    public void Unregister(int characterId, Messenger messenger)
    {
        lock (_gate)
        {
            _byCharacter.Remove(characterId);
            if (messenger.IsEmpty)
            {
                _byId.Remove(messenger.Id);
            }
        }
    }
}
