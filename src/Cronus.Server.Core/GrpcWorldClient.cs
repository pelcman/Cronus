using System.Net;
using Cronus.Server.Core.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using ProtoSource = Cronus.Server.Core.Grpc.MigrationSource;

namespace Cronus.Server.Core;

/// <summary>Conversions between the proto messages and the process-neutral records.</summary>
public static class ProtoConvert
{
    public static AirshipTimetable ToTimetable(Timetable t)
        => new(
            TimeSpan.FromSeconds(t.CycleSeconds),
            TimeSpan.FromSeconds(t.BoardingSeconds),
            TimeSpan.FromSeconds(t.RaidStartSeconds),
            TimeSpan.FromSeconds(t.RaidEndBeforeArrivalSeconds),
            t.BalrogChance);

    public static Timetable FromTimetable(AirshipTimetable t)
        => new()
        {
            CycleSeconds = (int)t.Cycle.TotalSeconds,
            BoardingSeconds = (int)t.Boarding.TotalSeconds,
            RaidStartSeconds = (int)t.RaidStart.TotalSeconds,
            RaidEndBeforeArrivalSeconds = (int)t.RaidEndBeforeArrival.TotalSeconds,
            BalrogChance = t.BalrogChance,
        };

    public static WorldView ToView(ChannelsResponse r)
    {
        var channels = new List<ChannelView>(r.Channels.Count);
        foreach (ChannelInfo c in r.Channels)
        {
            channels.Add(new ChannelView(c.Id, c.Name, new IPEndPoint(IPAddress.Parse(c.Host), c.Port), c.Online));
        }

        IPEndPoint? cashShop = r.CashShop is { Port: > 0 } cs ? new IPEndPoint(IPAddress.Parse(cs.Host), cs.Port) : null;
        return new WorldView(r.WorldName, channels, cashShop);
    }

    public static ChannelsResponse FromView(WorldView view)
    {
        var r = new ChannelsResponse { WorldName = view.Name };
        foreach (ChannelView c in view.Channels)
        {
            r.Channels.Add(new ChannelInfo { Id = c.Id, Name = c.Name, Host = c.Endpoint.Address.ToString(), Port = c.Endpoint.Port, Online = c.Online });
        }

        if (view.CashShop is not null)
        {
            r.CashShop = new Endpoint { Host = view.CashShop.Address.ToString(), Port = view.CashShop.Port };
        }

        return r;
    }

    public static ProtoSource FromSource(MigrationSource s) => s switch
    {
        MigrationSource.Login => ProtoSource.Login,
        MigrationSource.CashShop => ProtoSource.CashShop,
        _ => ProtoSource.Channel,
    };

    public static MigrationSource ToSource(ProtoSource s) => s switch
    {
        ProtoSource.Login => MigrationSource.Login,
        ProtoSource.CashShop => MigrationSource.CashShop,
        _ => MigrationSource.Channel,
    };
}

/// <summary>
/// The World as seen from a Login or Channel process, over gRPC. Every call degrades instead of
/// throwing: an unreachable World means "no channels", "no hand-off", "refused" — logged once
/// per outage — so a World restart never takes the other processes down with it.
/// </summary>
public sealed class GrpcWorldClient : IWorldClient, IAsyncDisposable
{
    /// <summary>The port the World listens on unless CRONUS_WORLD_PORT says otherwise.</summary>
    public const int DefaultPort = 8585;

    private readonly GrpcChannel _channel;
    private readonly World.WorldClient _client;
    private readonly Action<string> _log;
    private int _outage; // 1 while the World is unreachable, so the outage is logged once

    public GrpcWorldClient(Uri address, Action<string>? log = null)
    {
        Address = address;
        _channel = GrpcChannel.ForAddress(address);
        _client = new World.WorldClient(_channel);
        _log = log ?? Console.WriteLine;
    }

    public Uri Address { get; }

    /// <summary>CRONUS_WORLD_URI, else http://127.0.0.1:CRONUS_WORLD_PORT (default 8585).</summary>
    public static Uri ResolveAddress()
    {
        string? uri = Environment.GetEnvironmentVariable("CRONUS_WORLD_URI");
        if (!string.IsNullOrWhiteSpace(uri) && Uri.TryCreate(uri, UriKind.Absolute, out Uri? parsed))
        {
            return parsed;
        }

        int port = int.TryParse(Environment.GetEnvironmentVariable("CRONUS_WORLD_PORT"), out int p) && p > 0 ? p : DefaultPort;
        return new Uri($"http://127.0.0.1:{port}");
    }

    /// <summary>True when the World answers.</summary>
    public async ValueTask<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.ChannelsAsync(new Empty(), cancellationToken: cancellationToken).ConfigureAwait(false);
            Recovered();
            return true;
        }
        catch (RpcException ex)
        {
            Outage(ex);
            return false;
        }
    }

    /// <summary>
    /// Waits until the World answers, retrying <paramref name="attempts"/> times
    /// <paramref name="delay"/> apart (Maple2's Game does the same before it opens its port).
    /// </summary>
    public async Task<bool> WaitUntilReachableAsync(int attempts, TimeSpan delay, CancellationToken cancellationToken = default)
    {
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            if (await PingAsync(cancellationToken).ConfigureAwait(false))
            {
                return true;
            }

            _log($"[world] {Address} not answering — attempt {attempt}/{attempts}" + (attempt < attempts ? $", retrying in {delay.TotalSeconds:0}s" : string.Empty));
            if (attempt < attempts)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    public async ValueTask<WorldView> GetWorldAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            ChannelsResponse r = await _client.ChannelsAsync(new Empty(), cancellationToken: cancellationToken).ConfigureAwait(false);
            Recovered();
            return ProtoConvert.ToView(r);
        }
        catch (RpcException ex)
        {
            Outage(ex);
            return new WorldView("Cronus", Array.Empty<ChannelView>(), null);
        }
    }

    public async ValueTask<IPEndPoint?> MigrateOutAsync(int accountId, int characterId, int channel, MigrationSource source, CancellationToken cancellationToken = default)
    {
        try
        {
            MigrateOutResponse r = await _client.MigrateOutAsync(new MigrateOutRequest
            {
                AccountId = accountId,
                CharacterId = characterId,
                Channel = channel,
                Source = ProtoConvert.FromSource(source),
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
            Recovered();
            if (!r.Ok)
            {
                _log($"[world] migrate-out of character {characterId} to channel {channel} refused: {r.Reason}");
                return null;
            }

            return new IPEndPoint(IPAddress.Parse(r.Host), r.Port);
        }
        catch (RpcException ex)
        {
            Outage(ex);
            return null;
        }
    }

    public async ValueTask<MigrateInResult> MigrateInAsync(int characterId, int channel, CancellationToken cancellationToken = default)
    {
        try
        {
            MigrateInResponse r = await _client.MigrateInAsync(new MigrateInRequest { CharacterId = characterId, Channel = channel }, cancellationToken: cancellationToken).ConfigureAwait(false);
            Recovered();
            return r.Ok ? MigrateInResult.Accepted(r.AccountId) : MigrateInResult.Refused(r.Reason);
        }
        catch (RpcException ex)
        {
            Outage(ex);
            return MigrateInResult.Refused($"world unreachable ({ex.StatusCode})");
        }
    }

    public async ValueTask PlayerOfflineAsync(int characterId, int channel, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.PlayerOfflineAsync(new PlayerOfflineRequest { CharacterId = characterId, Channel = channel }, cancellationToken: cancellationToken).ConfigureAwait(false);
            Recovered();
        }
        catch (RpcException ex)
        {
            Outage(ex);
        }
    }

    public async ValueTask BroadcastAsync(byte[] packet, int excludeChannel = WorldState.CashShopChannel, CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.BroadcastAsync(new BroadcastRequest
            {
                Packet = Google.Protobuf.ByteString.CopyFrom(packet),
                ExcludeChannel = excludeChannel,
            }, cancellationToken: cancellationToken).ConfigureAwait(false);
            Recovered();
        }
        catch (RpcException ex)
        {
            Outage(ex);
        }
    }

    /// <summary>
    /// Registers this process's channels, retrying like <see cref="WaitUntilReachableAsync"/>.
    /// Null when the World never answered.
    /// </summary>
    public async Task<RegisterChannelsResponse?> RegisterChannelsAsync(
        string instanceId, IPAddress host, IReadOnlyList<int> ports, int? cashShopPort,
        int attempts = 3, TimeSpan? delay = null, CancellationToken cancellationToken = default)
    {
        var request = new RegisterChannelsRequest { InstanceId = instanceId, Host = host.ToString(), CashShopPort = cashShopPort ?? 0 };
        request.Ports.AddRange(ports);
        TimeSpan wait = delay ?? TimeSpan.FromSeconds(5);
        for (int attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                RegisterChannelsResponse r = await _client.RegisterChannelsAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
                Recovered();
                return r;
            }
            catch (RpcException ex)
            {
                Outage(ex);
                _log($"[world] could not register with {Address} — attempt {attempt}/{attempts}" + (attempt < attempts ? $", retrying in {wait.TotalSeconds:0}s" : string.Empty));
                if (attempt < attempts)
                {
                    await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return null;
    }

    /// <summary>True = alive, false = the World does not know us (register again), null = unreachable.</summary>
    public async Task<bool?> HeartbeatAsync(string instanceId, CancellationToken cancellationToken = default)
    {
        try
        {
            HeartbeatResponse r = await _client.HeartbeatAsync(new HeartbeatRequest { InstanceId = instanceId }, cancellationToken: cancellationToken).ConfigureAwait(false);
            Recovered();
            return r.Known;
        }
        catch (RpcException ex)
        {
            Outage(ex);
            return null;
        }
    }

    /// <summary>
    /// Receives the World's callbacks until cancelled, reconnecting whenever the stream drops
    /// (a World restart). <paramref name="onEvent"/> runs on the receiving task.
    /// </summary>
    public async Task SubscribeAsync(string instanceId, Func<WorldEvent, ValueTask> onEvent, CancellationToken cancellationToken)
    {
        TimeSpan backoff = TimeSpan.FromSeconds(1);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using AsyncServerStreamingCall<WorldEvent> call = _client.Subscribe(new SubscribeRequest { InstanceId = instanceId }, cancellationToken: cancellationToken);
                await foreach (WorldEvent ev in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
                {
                    Recovered();
                    backoff = TimeSpan.FromSeconds(1);
                    await onEvent(ev).ConfigureAwait(false);
                }
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled && cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (RpcException ex)
            {
                Outage(ex);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            try
            {
                await Task.Delay(backoff, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            backoff = TimeSpan.FromSeconds(Math.Min(backoff.TotalSeconds * 2, 10));
        }
    }

    private void Outage(RpcException ex)
    {
        if (Interlocked.Exchange(ref _outage, 1) == 0)
        {
            _log($"[world] {Address} unreachable ({ex.StatusCode}) — channel list, hand-offs and broadcasts are off until it is back");
        }
    }

    private void Recovered()
    {
        if (Interlocked.Exchange(ref _outage, 0) == 1)
        {
            _log($"[world] {Address} is back");
        }
    }

    public ValueTask DisposeAsync()
    {
        _channel.Dispose();
        return ValueTask.CompletedTask;
    }
}
