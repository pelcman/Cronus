using System.Collections.Concurrent;

namespace Cronus.Server.Game;

/// <summary>
/// One buff currently active on a character: the wire reason that identifies it (positive skill id /
/// negative item id), the mask word[0] bits it occupies, and when it lapses.
/// </summary>
public sealed record ActiveBuff(int Reason, ulong Mask, DateTime ExpiresAt);

/// <summary>
/// Server-side registry of every character's active temporary stats (ports the schedule kept by
/// <c>MapleCharacter.registerEffect</c>). The handler registers a buff whenever it sends
/// <c>LP_TemporaryStatSet</c>; <see cref="BuffExpiryService"/> sweeps it on a tick and pushes
/// <c>LP_TemporaryStatReset</c> for anything that lapsed, so buffs end even if the client lies.
/// </summary>
public sealed class BuffTracker
{
    private readonly ConcurrentDictionary<int, List<ActiveBuff>> _byCharacter = new();

    /// <summary>
    /// Registers (or refreshes) a buff. Re-casting replaces the entry with the same reason,
    /// like the reference's cancel-then-register.
    /// </summary>
    public void Register(int characterId, int reason, ulong mask, int durationMs, DateTime? now = null)
    {
        if (mask == 0 || durationMs <= 0)
        {
            return;
        }

        DateTime expiresAt = (now ?? DateTime.UtcNow).AddMilliseconds(durationMs);
        List<ActiveBuff> buffs = _byCharacter.GetOrAdd(characterId, _ => new List<ActiveBuff>());
        lock (buffs)
        {
            buffs.RemoveAll(b => b.Reason == reason);
            buffs.Add(new ActiveBuff(reason, mask, expiresAt));
        }
    }

    /// <summary>Removes one buff by reason (player cancelled it); returns its mask, or 0.</summary>
    public ulong Remove(int characterId, int reason)
    {
        if (!_byCharacter.TryGetValue(characterId, out List<ActiveBuff>? buffs))
        {
            return 0;
        }

        lock (buffs)
        {
            ulong mask = 0;
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (buffs[i].Reason == reason)
                {
                    mask |= buffs[i].Mask;
                    buffs.RemoveAt(i);
                }
            }

            return mask;
        }
    }

    /// <summary>The character's active buff with this reason, or null.</summary>
    public ActiveBuff? Find(int characterId, int reason)
    {
        if (!_byCharacter.TryGetValue(characterId, out List<ActiveBuff>? buffs))
        {
            return null;
        }

        lock (buffs)
        {
            return buffs.FirstOrDefault(b => b.Reason == reason);
        }
    }

    /// <summary>Drops all state for a character (logout).</summary>
    public void Clear(int characterId) => _byCharacter.TryRemove(characterId, out _);

    /// <summary>Removes and returns every buff that has lapsed as of <paramref name="now"/>.</summary>
    public List<ActiveBuff> TakeExpired(int characterId, DateTime now)
    {
        var expired = new List<ActiveBuff>();
        if (!_byCharacter.TryGetValue(characterId, out List<ActiveBuff>? buffs))
        {
            return expired;
        }

        lock (buffs)
        {
            for (int i = buffs.Count - 1; i >= 0; i--)
            {
                if (buffs[i].ExpiresAt <= now)
                {
                    expired.Add(buffs[i]);
                    buffs.RemoveAt(i);
                }
            }
        }

        return expired;
    }

    /// <summary>The character's active buffs (for tests / diagnostics).</summary>
    public IReadOnlyList<ActiveBuff> Snapshot(int characterId)
    {
        if (!_byCharacter.TryGetValue(characterId, out List<ActiveBuff>? buffs))
        {
            return Array.Empty<ActiveBuff>();
        }

        lock (buffs)
        {
            return buffs.ToArray();
        }
    }
}

/// <summary>
/// A server tick that expires lapsed buffs: for every online player, any tracked buff past its
/// duration is removed and the client receives <c>LP_TemporaryStatReset</c> with that buff's mask
/// (ports the per-effect <c>CancelEffectAction</c> the reference schedules on cast). Timed world
/// logic on a tick, decoupled from client packets (see CLAUDE.md §2).
/// </summary>
public sealed class BuffExpiryService
{
    private readonly FieldRegistry _fields;
    private readonly BuffTracker _buffs;
    private readonly ChannelPackets _packets;
    private readonly TimeSpan _interval;

    public BuffExpiryService(FieldRegistry fields, BuffTracker buffs, ChannelPackets packets, TimeSpan? interval = null)
    {
        _fields = fields;
        _buffs = buffs;
        _packets = packets;
        _interval = interval ?? TimeSpan.FromSeconds(1);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await TickAsync(DateTime.UtcNow).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// <summary>Expires lapsed buffs for every online player; returns how many resets were sent.</summary>
    public async ValueTask<int> TickAsync(DateTime now)
    {
        int sent = 0;
        foreach (Field field in _fields.Fields)
        {
            foreach (FieldPlayer player in field.Players)
            {
                List<ActiveBuff> expired = _buffs.TakeExpired(player.Character.Id, now);
                foreach (ActiveBuff buff in expired)
                {
                    try
                    {
                        await player.Session.SendAsync(_packets.TemporaryStatReset(buff.Mask)).ConfigureAwait(false);
                        sent++;
                    }
                    catch (Exception)
                    {
                        // a dying session shouldn't stop the sweep
                    }
                }
            }
        }

        return sent;
    }
}
