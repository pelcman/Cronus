using System.Net;
using Cronus.Server.Core;
using Cronus.Server.Core.Grpc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Cronus.Server.Core.Tests;

/// <summary>
/// The World service hosted on Kestrel (a random loopback port) and the client the Login and
/// Channel processes use, end to end: register, list, hand off, admit, heartbeat, subscribe,
/// broadcast — and how the client behaves when there is no World at all.
/// </summary>
public class WorldGrpcRoundTripTests
{
    private static async Task<(WebApplication App, WorldGrpcService Service, Uri Address)> StartWorldAsync()
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0, l => l.Protocols = HttpProtocols.Http2));
        var service = new WorldGrpcService(new WorldState("Test"), _ => { });
        builder.Services.AddGrpc();
        builder.Services.AddSingleton(service);
        WebApplication app = builder.Build();
        app.MapGrpcService<WorldGrpcService>();
        await app.StartAsync();
        return (app, service, new Uri(app.Urls.First()));
    }

    [Fact]
    public async Task ChannelProcessRegisters_LoginSeesIt_HandOffAndAdmissionRoundTrip()
    {
        (WebApplication app, WorldGrpcService service, Uri address) = await StartWorldAsync();
        await using var _ = app;
        await using var channelProcess = new GrpcWorldClient(address, _ => { });
        await using var loginProcess = new GrpcWorldClient(address, _ => { });

        RegisterChannelsResponse? registration = await channelProcess.RegisterChannelsAsync("proc-1", IPAddress.Loopback, new[] { 7575, 7576 }, cashShopPort: 7577, attempts: 1);
        Assert.NotNull(registration);
        Assert.Equal(new[] { 0, 1 }, registration!.ChannelIds);
        Assert.Equal("Test", registration.WorldName);
        Assert.Equal(900, registration.Timetable.CycleSeconds);
        Assert.Equal((int)WorldState.HeartbeatInterval.TotalSeconds, registration.HeartbeatSeconds);

        WorldView view = await loginProcess.GetWorldAsync();
        Assert.Equal("Test", view.Name);
        Assert.Equal(new[] { "Test-1", "Test-2" }, view.Channels.Select(c => c.Name));
        Assert.Equal(new IPEndPoint(IPAddress.Loopback, 7576), view.Channels[1].Endpoint);
        Assert.Equal(new IPEndPoint(IPAddress.Loopback, 7577), view.CashShop);

        // Login hands the character to channel 2; the Channel process admits it; the count moves.
        IPEndPoint? target = await loginProcess.MigrateOutAsync(accountId: 7, characterId: 42, channel: 1, MigrationSource.Login);
        Assert.Equal(new IPEndPoint(IPAddress.Loopback, 7576), target);
        MigrateInResult admitted = await channelProcess.MigrateInAsync(42, channel: 1);
        Assert.True(admitted.Ok);
        Assert.Equal(7, admitted.AccountId);
        Assert.Equal(1, (await loginProcess.GetWorldAsync()).Channels[1].Online);

        // Nobody sent character 43 anywhere.
        MigrateInResult stranger = await channelProcess.MigrateInAsync(43, channel: 0);
        Assert.False(stranger.Ok);
        Assert.Contains("no pending", stranger.Reason);

        await channelProcess.PlayerOfflineAsync(42, channel: 1);
        Assert.Equal(0, (await loginProcess.GetWorldAsync()).Channels[1].Online);

        Assert.True(await channelProcess.HeartbeatAsync("proc-1"));
        Assert.False(await channelProcess.HeartbeatAsync("proc-unknown"));
        Assert.Equal(0, service.SubscriberCount);
    }

    [Fact]
    public async Task Broadcast_ReachesASubscribedChannelProcess()
    {
        (WebApplication app, WorldGrpcService service, Uri address) = await StartWorldAsync();
        await using var _ = app;
        await using var channelProcess = new GrpcWorldClient(address, _ => { });
        await using var otherProcess = new GrpcWorldClient(address, _ => { });
        await channelProcess.RegisterChannelsAsync("proc-1", IPAddress.Loopback, new[] { 7575 }, null, attempts: 1);

        var received = new TaskCompletionSource<WorldEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        Task subscription = channelProcess.SubscribeAsync("proc-1", ev => { received.TrySetResult(ev); return ValueTask.CompletedTask; }, cts.Token);

        // The stream is open once the service counts the subscriber.
        DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        while (service.SubscriberCount == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }

        Assert.Equal(1, service.SubscriberCount);

        await otherProcess.BroadcastAsync(new byte[] { 0x11, 0x22, 0x33 }, excludeChannel: 5);
        WorldEvent ev = await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(WorldEvent.EventOneofCase.Broadcast, ev.EventCase);
        Assert.Equal(new byte[] { 0x11, 0x22, 0x33 }, ev.Broadcast.Packet.ToByteArray());
        Assert.Equal(5, ev.Broadcast.ExcludeChannel);

        // A kick reaches the process that hosts the ghost session.
        var kicked = new TaskCompletionSource<WorldEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        received = kicked;
        Task secondSubscription = Task.CompletedTask;
        Assert.True(service.Push("proc-1", new WorldEvent { Disconnect = new DisconnectPlayer { CharacterId = 99 } }));

        cts.Cancel();
        await subscription.WaitAsync(TimeSpan.FromSeconds(10));
        await secondSubscription;
    }

    [Fact]
    public async Task WithoutAWorld_TheClientDegradesInsteadOfThrowing()
    {
        // Nothing listens on port 1.
        var logged = new List<string>();
        await using var client = new GrpcWorldClient(new Uri("http://127.0.0.1:1"), logged.Add);

        WorldView view = await client.GetWorldAsync();
        Assert.Empty(view.Channels);
        Assert.Null(await client.MigrateOutAsync(1, 2, 0, MigrationSource.Login));
        MigrateInResult refused = await client.MigrateInAsync(2, 0);
        Assert.False(refused.Ok);
        Assert.Contains("unreachable", refused.Reason);
        Assert.Null(await client.HeartbeatAsync("proc-1"));
        Assert.Null(await client.RegisterChannelsAsync("proc-1", IPAddress.Loopback, new[] { 7575 }, null, attempts: 1));
        Assert.False(await client.PingAsync());

        // One outage line, however many calls failed.
        Assert.Single(logged.Where(l => l.Contains("unreachable")));
    }
}
