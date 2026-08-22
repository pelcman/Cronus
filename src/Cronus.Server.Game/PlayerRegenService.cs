using Cronus.Domain;

namespace Cronus.Server.Game;

/// <summary>Natural HP/MP recovery rules (pure, so they're easy to test).</summary>
public static class PlayerRegen
{
    /// <summary>How long a player must be idle (no move/attack) before regen starts.</summary>
    public const long IdleThresholdMs = 4000;

    /// <summary>
    /// Recovers a modest amount of HP/MP toward the maximum and returns the stats that changed
    /// (0 when already full). Mutates <paramref name="c"/>. Sitting (<paramref name="seated"/>)
    /// triples the amount; otherwise the map's <paramref name="recovery"/> multiplier applies
    /// (the reference skips it on chairs too). A simplification of MapleStory's level/job-scaled
    /// recovery.
    /// </summary>
    public static StatFlag Apply(Character c, bool seated = false, double recovery = 1.0)
    {
        double factor = seated ? 3 : recovery;
        StatFlag changed = 0;

        if (c.Hp < c.MaxHp)
        {
            c.Hp = (short)Math.Min(c.MaxHp, c.Hp + (int)(Math.Max(3, c.MaxHp / 50) * factor));
            changed |= StatFlag.Hp;
        }

        if (c.Mp < c.MaxMp)
        {
            c.Mp = (short)Math.Min(c.MaxMp, c.Mp + (int)(Math.Max(3, c.MaxMp / 50) * factor));
            changed |= StatFlag.Mp;
        }

        return changed;
    }
}

/// <summary>
/// A server tick that regenerates HP/MP for idle players and pushes the change with
/// <c>LP_StatChanged</c>. Timed world logic on a tick, decoupled from client packets
/// (see CLAUDE.md §2).
/// </summary>
public sealed class PlayerRegenService
{
    private readonly FieldRegistry _fields;
    private readonly ChannelPackets _packets;
    private readonly PartyRegistry? _parties;
    private readonly TimeSpan _interval;

    public PlayerRegenService(FieldRegistry fields, ChannelPackets packets, PartyRegistry? parties = null, TimeSpan? interval = null)
    {
        _fields = fields;
        _packets = packets;
        _parties = parties;
        _interval = interval ?? TimeSpan.FromSeconds(5);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                await TickAsync(Environment.TickCount64).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }

    /// <summary>Runs one regen pass at <paramref name="nowTick"/> (exposed for tests).</summary>
    public async Task TickAsync(long nowTick)
    {
        foreach (Field field in _fields.Fields)
        {
            foreach (FieldPlayer player in field.Players)
            {
                // Sitting rests immediately; otherwise wait for the idle threshold.
                if (!player.Seated && nowTick - player.LastActiveTick < PlayerRegen.IdleThresholdMs)
                {
                    continue; // recently moved or attacked — not resting
                }

                StatFlag changed = PlayerRegen.Apply(player.Character, player.Seated, field.Recovery);
                if (changed == 0)
                {
                    continue; // already at full HP/MP
                }

                try
                {
                    await player.Session.SendAsync(_packets.StatChanged(player.Character, changed)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // the session is going away; its disconnect path will clean up
                }

                // Recovered HP should tick up on party members' health bars too.
                if (changed.HasFlag(StatFlag.Hp))
                {
                    await PushHpToPartyAsync(player).ConfigureAwait(false);
                }
            }
        }
    }

    /// <summary>Pushes a regenerating player's HP bar to their same-map party members (if any).</summary>
    private async Task PushHpToPartyAsync(FieldPlayer player)
    {
        Party? party = _parties?.GetForCharacter(player.Character.Id);
        if (party is null)
        {
            return;
        }

        Character c = player.Character;
        byte[] hp = _packets.UserHP(c.Id, c.Hp, c.MaxHp);
        foreach (FieldPlayer member in party.Members)
        {
            if (member.Character.Id == c.Id || member.Character.MapId != c.MapId)
            {
                continue;
            }

            try
            {
                await member.Session.SendAsync(hp).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // dead session cleans up on its own path
            }
        }
    }
}
