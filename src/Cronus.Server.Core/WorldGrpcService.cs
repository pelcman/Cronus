using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Cronus.Server.Core.Grpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace Cronus.Server.Core;

/// <summary>
/// The World's gRPC face: a thin shell over <see cref="WorldState"/> plus the subscriber streams
/// that carry the World's callbacks (broadcasts, forced disconnects, timetable) to each Channel
/// process. Hosted by Cronus.Server.World; tests host it in-process on a random port.
/// </summary>
public sealed class WorldGrpcService : World.WorldBase
{
    private readonly WorldState _state;
    private readonly Action<string> _log;
    private readonly ConcurrentDictionary<string, Channel<WorldEvent>> _subscribers = new(StringComparer.Ordinal);

    public WorldGrpcService(WorldState state, Action<string>? log = null)
    {
        _state = state;
        _log = log ?? Console.WriteLine;
    }

    public WorldState State => _state;

    public int SubscriberCount => _subscribers.Count;

    public override Task<RegisterChannelsResponse> RegisterChannels(RegisterChannelsRequest request, ServerCallContext context)
    {
        IPAddress host = IPAddress.TryParse(request.Host, out IPAddress? parsed) ? parsed : IPAddress.Loopback;
        IReadOnlyList<int> ids = _state.RegisterChannels(request.InstanceId, host, request.Ports, request.CashShopPort > 0 ? request.CashShopPort : null);
        _log($"[world] channel process {request.InstanceId} registered: channels {string.Join(",", ids.Select(i => i + 1))} on {host} ports {string.Join(",", request.Ports)}"
             + (request.CashShopPort > 0 ? $", cash shop {request.CashShopPort}" : string.Empty));

        var response = new RegisterChannelsResponse
        {
            WorldName = _state.Name,
            HeartbeatSeconds = (int)WorldState.HeartbeatInterval.TotalSeconds,
            Timetable = ProtoConvert.FromTimetable(_state.Timetable),
        };
        response.ChannelIds.AddRange(ids);
        return Task.FromResult(response);
    }

    public override Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
        => Task.FromResult(new HeartbeatResponse { Known = _state.Heartbeat(request.InstanceId) });

    public override Task<ChannelsResponse> Channels(Empty request, ServerCallContext context)
        => Task.FromResult(ProtoConvert.FromView(_state.Channels()));

    public override Task<MigrateOutResponse> MigrateOut(MigrateOutRequest request, ServerCallContext context)
    {
        (IPEndPoint? endpoint, string? reason) = _state.MigrateOut(request.AccountId, request.CharacterId, request.Channel, ProtoConvert.ToSource(request.Source));
        if (endpoint is null)
        {
            _log($"[world] migrate-out refused: character {request.CharacterId} → channel {request.Channel}: {reason}");
            return Task.FromResult(new MigrateOutResponse { Ok = false, Reason = reason ?? string.Empty });
        }

        return Task.FromResult(new MigrateOutResponse { Ok = true, Host = endpoint.Address.ToString(), Port = endpoint.Port, Channel = request.Channel });
    }

    public override Task<MigrateInResponse> MigrateIn(MigrateInRequest request, ServerCallContext context)
    {
        MigrateInResult result = _state.MigrateIn(request.CharacterId, request.Channel, out WorldState.Kick? kick);
        if (kick is { } k)
        {
            // The character is still online where it came from (a ghost session): drop it there.
            _log($"[world] character {k.CharacterId} logged in again — disconnecting its session on {k.InstanceId}");
            Push(k.InstanceId, new WorldEvent { Disconnect = new DisconnectPlayer { CharacterId = k.CharacterId } });
        }

        if (!result.Ok)
        {
            _log($"[world] migrate-in refused: character {request.CharacterId} at channel {request.Channel}: {result.Reason}");
        }

        return Task.FromResult(new MigrateInResponse { Ok = result.Ok, AccountId = result.AccountId, Reason = result.Reason ?? string.Empty });
    }

    public override Task<Empty> PlayerOffline(PlayerOfflineRequest request, ServerCallContext context)
    {
        _state.PlayerOffline(request.CharacterId, request.Channel);
        return Task.FromResult(new Empty());
    }

    public override Task<Empty> Broadcast(BroadcastRequest request, ServerCallContext context)
    {
        var ev = new WorldEvent { Broadcast = request };
        foreach (Channel<WorldEvent> subscriber in _subscribers.Values)
        {
            subscriber.Writer.TryWrite(ev);
        }

        return Task.FromResult(new Empty());
    }

    public override async Task Subscribe(SubscribeRequest request, IServerStreamWriter<WorldEvent> responseStream, ServerCallContext context)
    {
        var events = Channel.CreateUnbounded<WorldEvent>(new UnboundedChannelOptions { SingleReader = true });
        _subscribers[request.InstanceId] = events;
        _log($"[world] channel process {request.InstanceId} subscribed to world events");
        try
        {
            await foreach (WorldEvent ev in events.Reader.ReadAllAsync(context.CancellationToken).ConfigureAwait(false))
            {
                await responseStream.WriteAsync(ev, context.CancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // the Channel process went away
        }
        finally
        {
            _subscribers.TryRemove(new KeyValuePair<string, Channel<WorldEvent>>(request.InstanceId, events));
            _log($"[world] channel process {request.InstanceId} unsubscribed");
        }
    }

    /// <summary>Queues an event for one Channel process; dropped when it is not subscribed.</summary>
    public bool Push(string instanceId, WorldEvent ev)
        => _subscribers.TryGetValue(instanceId, out Channel<WorldEvent>? subscriber) && subscriber.Writer.TryWrite(ev);

    /// <summary>Drops silent Channel processes once a second and says so.</summary>
    public async Task RunMaintenanceAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (string dropped in _state.PruneExpired())
                {
                    _log($"[world] channel process {dropped} stopped answering — its channels and players are gone until it registers again");
                    if (_subscribers.TryRemove(dropped, out Channel<WorldEvent>? subscriber))
                    {
                        subscriber.Writer.TryComplete();
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // shutdown
        }
    }
}
