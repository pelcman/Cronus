namespace Cronus.Server.Channel;

/// <summary>
/// A server tick that brings dead mobs back after a delay: each tick it respawns every mob whose
/// scheduled time has arrived, announces <c>LP_MobEnterField</c> to the field, and hands control
/// to a player present (<c>LP_MobChangeController</c>). This keeps hunting maps populated instead
/// of emptying out as mobs are killed. Timed world logic like this belongs on a tick, decoupled
/// from client packets (see CLAUDE.md §2 networking notes).
/// </summary>
public sealed class MobRespawnService
{
    /// <summary>
    /// How long after death a mob respawns. A simplification of the per-spawn <c>mobTime</c> —
    /// a fixed delay for now; wz-driven per-mob timers are a follow-up.
    /// </summary>
    public const long DelayMs = 7000;

    /// <summary>
    /// The <see cref="Environment.TickCount64"/> at which a killed mob should respawn, given its
    /// map <c>mobTime</c> (seconds): &gt;0 = that delay, -1 = never (returns 0 → no respawn),
    /// 0/absent = the default <see cref="DelayMs"/>.
    /// </summary>
    public static long NextRespawnTick(int mobTimeSeconds)
    {
        if (mobTimeSeconds == -1)
        {
            return 0; // one-shot / boss: no respawn
        }

        long delayMs = mobTimeSeconds > 0 ? mobTimeSeconds * 1000L : DelayMs;
        return Environment.TickCount64 + delayMs;
    }

    private readonly FieldRegistry _fields;
    private readonly ChannelPackets _packets;
    private readonly TimeSpan _interval;

    public MobRespawnService(FieldRegistry fields, ChannelPackets packets, TimeSpan? interval = null)
    {
        _fields = fields;
        _packets = packets;
        _interval = interval ?? TimeSpan.FromSeconds(2);
    }

    /// <summary>Runs the respawn tick until cancelled.</summary>
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

    /// <summary>Runs one respawn pass at <paramref name="nowTick"/> (exposed for tests).</summary>
    public async Task TickAsync(long nowTick)
    {
        foreach (Field field in _fields.Fields)
        {
            IReadOnlyList<FieldMob> respawned = field.TakeRespawnDueMobs(nowTick);
            if (respawned.Count == 0)
            {
                continue;
            }

            // Hand the respawned mobs to a player in the field, if any, for client-side AI.
            FieldPlayer? controller = field.Players.Count > 0 ? field.Players[0] : null;

            foreach (FieldMob mob in respawned)
            {
                await field.BroadcastAsync(_packets.MobEnterField(mob)).ConfigureAwait(false);

                if (controller is not null)
                {
                    mob.ControllerId = controller.Character.Id;
                    try
                    {
                        await controller.Session.SendAsync(_packets.MobChangeController(mob)).ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        mob.ControllerId = -1; // the controller is going away
                    }
                }
            }
        }
    }
}
