using System.Net;

namespace Cronus.Server.Core;

/// <summary>
/// What the Login and Channel processes need from the World: the channel list, the hand-offs
/// between processes, presence, and world-wide broadcasts. <see cref="GrpcWorldClient"/> talks to
/// the World process; <see cref="LocalWorld"/> runs the same state in-process (tests, and a
/// handler constructed without a World).
/// </summary>
public interface IWorldClient
{
    /// <summary>The channels to show at world select, and the cash shop if one runs.</summary>
    ValueTask<WorldView> GetWorldAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Announces that the character is being sent to <paramref name="channel"/>
    /// (<see cref="WorldState.CashShopChannel"/> for the cash shop) and returns where that is;
    /// null when no such channel is running.
    /// </summary>
    ValueTask<IPEndPoint?> MigrateOutAsync(int accountId, int characterId, int channel, MigrationSource source, CancellationToken cancellationToken = default);

    /// <summary>Asks whether the character arriving on <paramref name="channel"/> was really sent there.</summary>
    ValueTask<MigrateInResult> MigrateInAsync(int characterId, int channel, CancellationToken cancellationToken = default);

    /// <summary>The character's session on <paramref name="channel"/> ended.</summary>
    ValueTask PlayerOfflineAsync(int characterId, int channel, CancellationToken cancellationToken = default);

    /// <summary>Delivers one packet to every player on every channel of the world.</summary>
    ValueTask BroadcastAsync(byte[] packet, int excludeChannel = WorldState.CashShopChannel, CancellationToken cancellationToken = default);
}

/// <summary>
/// The world inside one process: the same <see cref="WorldState"/> the World process runs, fed
/// with a fixed channel list. This is what a handler gets when it is built without a World
/// (tests, the headless exercisers), and it keeps the pre-split behaviour: every channel is on
/// this process, hand-offs always succeed unless <c>strict</c> is on.
/// </summary>
public sealed class LocalWorld : IWorldClient
{
    private readonly bool _strict;

    public LocalWorld(IReadOnlyList<IPEndPoint> channels, IPEndPoint? cashShop = null, string name = "Cronus", bool strict = false)
    {
        _strict = strict;
        State = new WorldState(name, instanceTimeout: TimeSpan.MaxValue);
        if (channels.Count == 0)
        {
            channels = new[] { new IPEndPoint(IPAddress.Loopback, 7575) };
        }

        // One instance per endpoint keeps the ids in list order whatever the addresses are.
        for (int i = 0; i < channels.Count; i++)
        {
            State.RegisterChannels($"local-{i}", channels[i].Address, new[] { channels[i].Port }, i == 0 ? cashShop?.Port : null);
        }
    }

    /// <summary>The in-process state, for tests that want to look at presence or pending hand-offs.</summary>
    public WorldState State { get; }

    /// <summary>In-process delivery of <see cref="BroadcastAsync"/>; set by the owner of the fields.</summary>
    public Func<byte[], int, ValueTask>? Broadcast { get; set; }

    public ValueTask<WorldView> GetWorldAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(State.Channels());

    public ValueTask<IPEndPoint?> MigrateOutAsync(int accountId, int characterId, int channel, MigrationSource source, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(State.MigrateOut(accountId, characterId, channel, source).Endpoint);

    public ValueTask<MigrateInResult> MigrateInAsync(int characterId, int channel, CancellationToken cancellationToken = default)
    {
        MigrateInResult result = State.MigrateIn(characterId, channel, out _);
        return ValueTask.FromResult(_strict || result.Ok ? result : MigrateInResult.Accepted(0));
    }

    public ValueTask PlayerOfflineAsync(int characterId, int channel, CancellationToken cancellationToken = default)
    {
        State.PlayerOffline(characterId, channel);
        return ValueTask.CompletedTask;
    }

    public ValueTask BroadcastAsync(byte[] packet, int excludeChannel = WorldState.CashShopChannel, CancellationToken cancellationToken = default)
        => Broadcast?.Invoke(packet, excludeChannel) ?? ValueTask.CompletedTask;
}
