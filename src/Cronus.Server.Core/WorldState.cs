using System.Net;

namespace Cronus.Server.Core;

/// <summary>One channel as the Login shows it: id, name, where to connect, how many are on it.</summary>
public sealed record ChannelView(int Id, string Name, IPEndPoint Endpoint, int Online);

/// <summary>The world as the Login and the Channel processes see it.</summary>
public sealed record WorldView(string Name, IReadOnlyList<ChannelView> Channels, IPEndPoint? CashShop)
{
    public ChannelView? Find(int channelId)
    {
        foreach (ChannelView channel in Channels)
        {
            if (channel.Id == channelId)
            {
                return channel;
            }
        }

        return null;
    }
}

/// <summary>Who is handing the character over.</summary>
public enum MigrationSource
{
    Login,
    Channel,
    CashShop,
}

/// <summary>The World's answer to "was this character really sent to me?".</summary>
public readonly record struct MigrateInResult(bool Ok, int AccountId, string? Reason)
{
    public static MigrateInResult Accepted(int accountId) => new(true, accountId, null);

    public static MigrateInResult Refused(string reason) => new(false, 0, reason);
}

/// <summary>
/// The airship timetable, owned by the World so every channel runs the same clock. Pure
/// wall-clock arithmetic on the Channel side (see <c>AirshipSchedule</c>), so nothing has to be
/// synchronised beyond these numbers.
/// </summary>
public sealed record AirshipTimetable(
    TimeSpan Cycle,
    TimeSpan Boarding,
    TimeSpan RaidStart,
    TimeSpan RaidEndBeforeArrival,
    double BalrogChance)
{
    /// <summary>The authored default: a 15-minute cycle, boarding for 10, a raid every flight.</summary>
    public static AirshipTimetable Default { get; } = new(
        TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(10), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(30), 1.0);
}

/// <summary>
/// The World's shared state, in memory and process-neutral: which Channel processes exist and
/// which channels they run, who is online where, and the pending hand-offs between processes.
/// Thread-safe; every public member takes the one lock. The gRPC service
/// (<see cref="WorldGrpcService"/>) is a thin shell over it, and <see cref="LocalWorld"/> runs the
/// same object in-process for tests and single-process use.
/// </summary>
public sealed class WorldState
{
    /// <summary>The channel id that means "the cash shop" in migrations.</summary>
    public const int CashShopChannel = -1;

    /// <summary>Channel processes that stay silent this long are dropped, players included.</summary>
    public static readonly TimeSpan DefaultInstanceTimeout = TimeSpan.FromSeconds(15);

    /// <summary>How often a Channel process must call <see cref="Heartbeat"/>.</summary>
    public static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(5);

    /// <summary>A character that was migrated out must arrive within this window.</summary>
    public static readonly TimeSpan MigrationTimeout = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private readonly Func<DateTime> _clock;
    private readonly Dictionary<string, Instance> _instances = new(StringComparer.Ordinal);
    private readonly Dictionary<int, Pending> _pending = new();       // by character id
    private readonly Dictionary<int, OnlinePlayer> _online = new();   // by character id

    public WorldState(string name = "Cronus", AirshipTimetable? timetable = null, TimeSpan? instanceTimeout = null, Func<DateTime>? clock = null)
    {
        Name = name;
        Timetable = timetable ?? AirshipTimetable.Default;
        InstanceTimeout = instanceTimeout ?? DefaultInstanceTimeout;
        _clock = clock ?? (() => DateTime.UtcNow);
    }

    public string Name { get; }

    public AirshipTimetable Timetable { get; }

    public TimeSpan InstanceTimeout { get; }

    /// <summary>A Channel process and the channels it runs.</summary>
    private sealed class Instance
    {
        public required string Id { get; init; }
        public required IPAddress Host { get; init; }
        public required List<int> ChannelIds { get; init; }
        public required List<int> Ports { get; init; }
        public int? CashShopPort { get; init; }
        public DateTime LastSeen { get; set; }
    }

    private sealed record Pending(int AccountId, int Channel, DateTime Expires);

    private sealed record OnlinePlayer(int AccountId, int Channel, string InstanceId);

    /// <summary>Where a kicked character was, so the service can tell that process.</summary>
    public readonly record struct Kick(string InstanceId, int CharacterId);

    /// <summary>
    /// A Channel process announces its listeners. Channel ids are the lowest free ones, so a
    /// restarted process gets its old numbers back; re-registering the same instance id (after a
    /// World restart) reuses the ids it had when the port count is unchanged. A process that is
    /// still registered on the same host and ports cannot be alive (the ports are taken by the
    /// newcomer), so it is dropped first — a restart within the heartbeat window keeps its numbers.
    /// </summary>
    public IReadOnlyList<int> RegisterChannels(string instanceId, IPAddress host, IReadOnlyList<int> ports, int? cashShopPort)
    {
        if (ports.Count == 0)
        {
            throw new ArgumentException("a Channel process must announce at least one port", nameof(ports));
        }

        lock (_gate)
        {
            PruneLocked();
            foreach (Instance stale in _instances.Values.Where(i => i.Id != instanceId && i.Host.Equals(host) && i.Ports.Intersect(ports).Any()).ToArray())
            {
                DropInstanceLocked(stale);
            }

            List<int> ids;
            if (_instances.TryGetValue(instanceId, out Instance? existing) && existing.ChannelIds.Count == ports.Count)
            {
                ids = existing.ChannelIds;
            }
            else
            {
                if (existing is not null)
                {
                    DropInstanceLocked(existing);
                }

                var used = new HashSet<int>(_instances.Values.SelectMany(i => i.ChannelIds));
                ids = new List<int>(ports.Count);
                for (int next = 0; ids.Count < ports.Count; next++)
                {
                    if (!used.Contains(next))
                    {
                        ids.Add(next);
                    }
                }
            }

            _instances[instanceId] = new Instance
            {
                Id = instanceId,
                Host = host,
                ChannelIds = ids,
                Ports = ports.ToList(),
                CashShopPort = cashShopPort is > 0 ? cashShopPort : null,
                LastSeen = _clock(),
            };
            return ids.ToArray();
        }
    }

    /// <summary>Marks the process alive; false when the World does not know it (register again).</summary>
    public bool Heartbeat(string instanceId)
    {
        lock (_gate)
        {
            if (!_instances.TryGetValue(instanceId, out Instance? instance))
            {
                return false;
            }

            instance.LastSeen = _clock();
            return true;
        }
    }

    /// <summary>Drops the process and everything it hosted (its players go offline).</summary>
    public void Unregister(string instanceId)
    {
        lock (_gate)
        {
            if (_instances.TryGetValue(instanceId, out Instance? instance))
            {
                DropInstanceLocked(instance);
            }
        }
    }

    /// <summary>The channel list, by id, with the World's own online counts.</summary>
    public WorldView Channels()
    {
        lock (_gate)
        {
            PruneLocked();
            var channels = new List<ChannelView>();
            IPEndPoint? cashShop = null;
            foreach (Instance instance in _instances.Values)
            {
                for (int i = 0; i < instance.ChannelIds.Count; i++)
                {
                    int id = instance.ChannelIds[i];
                    int online = _online.Values.Count(p => p.Channel == id);
                    channels.Add(new ChannelView(id, $"{Name}-{id + 1}", new IPEndPoint(instance.Host, instance.Ports[i]), online));
                }

                if (instance.CashShopPort is int csPort)
                {
                    cashShop ??= new IPEndPoint(instance.Host, csPort);
                }
            }

            channels.Sort((a, b) => a.Id.CompareTo(b.Id));
            return new WorldView(Name, channels, cashShop);
        }
    }

    /// <summary>
    /// Records that <paramref name="characterId"/> is being sent to <paramref name="channel"/>
    /// (<see cref="CashShopChannel"/> for the cash shop) and returns where that is. Null with a
    /// reason when there is no such channel right now.
    /// </summary>
    public (IPEndPoint? Endpoint, string? Reason) MigrateOut(int accountId, int characterId, int channel, MigrationSource source)
    {
        lock (_gate)
        {
            PruneLocked();
            IPEndPoint? endpoint = channel == CashShopChannel ? CashShopEndpointLocked(characterId) : ChannelEndpointLocked(channel);
            if (endpoint is null)
            {
                return (null, channel == CashShopChannel ? "no cash shop is running" : $"channel {channel} is not running");
            }

            _pending[characterId] = new Pending(accountId, channel, _clock() + MigrationTimeout);
            return (endpoint, null);
        }
    }

    /// <summary>
    /// The receiving process asks whether <paramref name="characterId"/> was sent to
    /// <paramref name="channel"/>. Accepting marks the character online there; a copy still online
    /// elsewhere is reported as <paramref name="kick"/> so that process can drop it.
    /// </summary>
    public MigrateInResult MigrateIn(int characterId, int channel, out Kick? kick)
    {
        kick = null;
        lock (_gate)
        {
            PruneLocked();
            if (!_pending.TryGetValue(characterId, out Pending? pending))
            {
                return MigrateInResult.Refused("no pending migration for this character");
            }

            if (pending.Channel != channel)
            {
                return MigrateInResult.Refused($"character was sent to channel {pending.Channel}, not {channel}");
            }

            _pending.Remove(characterId);
            string? instanceId = channel == CashShopChannel ? CashShopInstanceLocked() : InstanceOfChannelLocked(channel)?.Id;
            if (instanceId is null)
            {
                return MigrateInResult.Refused($"channel {channel} is not running");
            }

            if (_online.TryGetValue(characterId, out OnlinePlayer? old) && old.Channel != channel)
            {
                kick = new Kick(old.InstanceId, characterId);
            }

            _online[characterId] = new OnlinePlayer(pending.AccountId, channel, instanceId);
            return MigrateInResult.Accepted(pending.AccountId);
        }
    }

    /// <summary>
    /// The character's session on <paramref name="channel"/> ended. Only that channel's record is
    /// removed: after a channel change the old channel reports offline after the new one has
    /// already reported the arrival, and that must not erase the new record.
    /// </summary>
    public void PlayerOffline(int characterId, int channel)
    {
        lock (_gate)
        {
            if (_online.TryGetValue(characterId, out OnlinePlayer? player) && player.Channel == channel)
            {
                _online.Remove(characterId);
            }
        }
    }

    /// <summary>Where the character is right now, or null when offline.</summary>
    public int? ChannelOf(int characterId)
    {
        lock (_gate)
        {
            return _online.TryGetValue(characterId, out OnlinePlayer? p) ? p.Channel : null;
        }
    }

    public int OnlineCount(int channel)
    {
        lock (_gate)
        {
            return _online.Values.Count(p => p.Channel == channel);
        }
    }

    public int OnlineTotal()
    {
        lock (_gate)
        {
            return _online.Count;
        }
    }

    /// <summary>The instance ids that are currently registered (alive).</summary>
    public IReadOnlyList<string> Instances()
    {
        lock (_gate)
        {
            PruneLocked();
            return _instances.Keys.ToArray();
        }
    }

    /// <summary>Drops silent processes and stale hand-offs; returns the dropped instance ids.</summary>
    public IReadOnlyList<string> PruneExpired()
    {
        lock (_gate)
        {
            return PruneLocked();
        }
    }

    private List<string> PruneLocked()
    {
        DateTime now = _clock();
        var dropped = new List<string>();
        foreach (Instance instance in _instances.Values.ToArray())
        {
            if (now - instance.LastSeen > InstanceTimeout)
            {
                DropInstanceLocked(instance);
                dropped.Add(instance.Id);
            }
        }

        foreach ((int characterId, Pending pending) in _pending.ToArray())
        {
            if (pending.Expires < now)
            {
                _pending.Remove(characterId);
            }
        }

        return dropped;
    }

    private void DropInstanceLocked(Instance instance)
    {
        _instances.Remove(instance.Id);
        foreach ((int characterId, OnlinePlayer player) in _online.ToArray())
        {
            if (player.InstanceId == instance.Id)
            {
                _online.Remove(characterId);
            }
        }
    }

    private Instance? InstanceOfChannelLocked(int channel)
        => _instances.Values.FirstOrDefault(i => i.ChannelIds.Contains(channel));

    private IPEndPoint? ChannelEndpointLocked(int channel)
    {
        Instance? instance = InstanceOfChannelLocked(channel);
        return instance is null ? null : new IPEndPoint(instance.Host, instance.Ports[instance.ChannelIds.IndexOf(channel)]);
    }

    /// <summary>The cash shop of the process the character is on, else any process that runs one.</summary>
    private IPEndPoint? CashShopEndpointLocked(int characterId)
    {
        Instance? preferred = _online.TryGetValue(characterId, out OnlinePlayer? p) && _instances.TryGetValue(p.InstanceId, out Instance? own) && own.CashShopPort is not null
            ? own
            : _instances.Values.FirstOrDefault(i => i.CashShopPort is not null);
        return preferred is null ? null : new IPEndPoint(preferred.Host, preferred.CashShopPort!.Value);
    }

    private string? CashShopInstanceLocked()
        => _instances.Values.FirstOrDefault(i => i.CashShopPort is not null)?.Id;
}
