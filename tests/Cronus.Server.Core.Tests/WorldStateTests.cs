using System.Net;
using Cronus.Server.Core;
using Xunit;

namespace Cronus.Server.Core.Tests;

/// <summary>The World's shared state, driven with a hand-turned clock.</summary>
public class WorldStateTests
{
    private static readonly IPAddress Host = IPAddress.Parse("10.0.0.5");

    private sealed class Clock
    {
        public DateTime Now { get; set; } = new(2026, 9, 9, 12, 0, 0, DateTimeKind.Utc);

        public void Advance(TimeSpan by) => Now += by;
    }

    private static (WorldState World, Clock Clock) NewWorld()
    {
        var clock = new Clock();
        return (new WorldState("Cronus", clock: () => clock.Now), clock);
    }

    [Fact]
    public void RegisterChannels_AssignsTheLowestFreeIds_AndReusesThemAfterAProcessLeaves()
    {
        (WorldState world, _) = NewWorld();

        Assert.Equal(new[] { 0, 1 }, world.RegisterChannels("proc-a", Host, new[] { 7575, 7576 }, cashShopPort: 7577));
        Assert.Equal(new[] { 2 }, world.RegisterChannels("proc-b", Host, new[] { 7580 }, cashShopPort: null));

        // The same process registering again (after a World restart) keeps its numbers.
        Assert.Equal(new[] { 0, 1 }, world.RegisterChannels("proc-a", Host, new[] { 7575, 7576 }, cashShopPort: 7577));

        world.Unregister("proc-a");
        Assert.Equal(new[] { 0, 1 }, world.RegisterChannels("proc-c", Host, new[] { 7590, 7591 }, cashShopPort: null));

        WorldView view = world.Channels();
        Assert.Equal(new[] { 0, 1, 2 }, view.Channels.Select(c => c.Id));
        Assert.Equal(new[] { "Cronus-1", "Cronus-2", "Cronus-3" }, view.Channels.Select(c => c.Name));
        Assert.Equal(7590, view.Channels[0].Endpoint.Port);
        Assert.Equal(7580, view.Channels[2].Endpoint.Port);
        Assert.Null(view.CashShop); // proc-a took the only cash shop with it
    }

    [Fact]
    public void ARestartedProcess_TakesOverTheStaleRegistrationOnItsPorts_AndKeepsTheNumbers()
    {
        (WorldState world, _) = NewWorld();
        world.RegisterChannels("pid-100", Host, new[] { 7575, 7576 }, cashShopPort: 7577);
        world.MigrateOut(1, 10, 0, MigrationSource.Login);
        world.MigrateIn(10, 0, out _);

        // The process was killed and started again (new pid) before the heartbeat window ran out:
        // the same ports cannot be listened on twice, so the old registration is dead.
        Assert.Equal(new[] { 0, 1 }, world.RegisterChannels("pid-200", Host, new[] { 7575, 7576 }, cashShopPort: 7577));
        Assert.Equal(new[] { "pid-200" }, world.Instances());
        Assert.Equal(0, world.OnlineTotal());          // its players went with it

        // A different host with the same ports is another machine: both stay.
        world.RegisterChannels("other-box", IPAddress.Parse("10.0.0.6"), new[] { 7575 }, cashShopPort: null);
        Assert.Equal(2, world.Instances().Count);
    }

    [Fact]
    public void MigrateOutThenIn_MarksTheCharacterOnline_AndTheLoginSeesTheCount()
    {
        (WorldState world, _) = NewWorld();
        world.RegisterChannels("proc-a", Host, new[] { 7575, 7576 }, cashShopPort: 7577);

        (IPEndPoint? endpoint, string? reason) = world.MigrateOut(accountId: 1, characterId: 10, channel: 1, MigrationSource.Login);
        Assert.Null(reason);
        Assert.Equal(new IPEndPoint(Host, 7576), endpoint);

        MigrateInResult result = world.MigrateIn(10, channel: 1, out WorldState.Kick? kick);
        Assert.True(result.Ok);
        Assert.Equal(1, result.AccountId);
        Assert.Null(kick);

        WorldView view = world.Channels();
        Assert.Equal(0, view.Channels[0].Online);
        Assert.Equal(1, view.Channels[1].Online);
        Assert.Equal(1, world.ChannelOf(10));
    }

    [Fact]
    public void MigrateIn_IsRefused_WithoutAHandOff_ForTheWrongChannel_OrTooLate()
    {
        (WorldState world, Clock clock) = NewWorld();
        world.RegisterChannels("proc-a", Host, new[] { 7575, 7576 }, cashShopPort: null);

        Assert.False(world.MigrateIn(10, 0, out _).Ok);

        world.MigrateOut(1, 10, channel: 1, MigrationSource.Login);
        Assert.False(world.MigrateIn(10, channel: 0, out _).Ok);   // sent to 1, arrived at 0

        Assert.True(world.MigrateIn(10, channel: 1, out _).Ok);
        Assert.False(world.MigrateIn(10, channel: 1, out _).Ok);   // a hand-off is used once

        world.MigrateOut(1, 11, channel: 0, MigrationSource.Login);
        clock.Advance(WorldState.MigrationTimeout + TimeSpan.FromSeconds(1));
        Assert.False(world.MigrateIn(11, channel: 0, out _).Ok);
        // the heartbeat keeps the process itself alive through that wait
    }

    [Fact]
    public void ASilentProcess_IsDroppedWithItsPlayers_AndMustRegisterAgain()
    {
        (WorldState world, Clock clock) = NewWorld();
        world.RegisterChannels("proc-a", Host, new[] { 7575 }, cashShopPort: null);
        world.MigrateOut(1, 10, 0, MigrationSource.Login);
        world.MigrateIn(10, 0, out _);
        Assert.Equal(1, world.OnlineTotal());

        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.True(world.Heartbeat("proc-a"));
        clock.Advance(TimeSpan.FromSeconds(10));
        Assert.Single(world.Channels().Channels);  // 10 s since the last heartbeat: still here

        clock.Advance(TimeSpan.FromSeconds(10));    // 20 s: gone
        Assert.Equal(new[] { "proc-a" }, world.PruneExpired());
        Assert.Empty(world.Channels().Channels);
        Assert.Equal(0, world.OnlineTotal());
        Assert.False(world.Heartbeat("proc-a"));   // "register again"
        Assert.False(world.Heartbeat("never-seen"));
    }

    [Fact]
    public void PlayerOffline_OnlyClearsTheChannelItNames_SoALateReportCannotEraseANewArrival()
    {
        (WorldState world, _) = NewWorld();
        world.RegisterChannels("proc-a", Host, new[] { 7575, 7576 }, cashShopPort: null);
        world.MigrateOut(1, 10, 0, MigrationSource.Login);
        world.MigrateIn(10, 0, out _);

        // Channel change: the new channel admits first, the old one reports offline afterwards.
        world.MigrateOut(1, 10, 1, MigrationSource.Channel);
        world.MigrateIn(10, 1, out _);
        world.PlayerOffline(10, channel: 0);
        Assert.Equal(1, world.ChannelOf(10));

        world.PlayerOffline(10, channel: 1);
        Assert.Null(world.ChannelOf(10));
    }

    [Fact]
    public void ASecondLogin_KicksTheCopyStillOnlineOnAnotherProcess()
    {
        (WorldState world, _) = NewWorld();
        world.RegisterChannels("proc-a", Host, new[] { 7575 }, cashShopPort: null);
        world.RegisterChannels("proc-b", Host, new[] { 7580 }, cashShopPort: null);
        world.MigrateOut(1, 10, 0, MigrationSource.Login);
        world.MigrateIn(10, 0, out _);

        world.MigrateOut(1, 10, 1, MigrationSource.Login);
        Assert.True(world.MigrateIn(10, 1, out WorldState.Kick? kick).Ok);
        Assert.Equal(new WorldState.Kick("proc-a", 10), kick);
        Assert.Equal(1, world.ChannelOf(10));
    }

    [Fact]
    public void TheCashShop_IsAChannelOfItsOwn()
    {
        (WorldState world, _) = NewWorld();
        world.RegisterChannels("proc-a", Host, new[] { 7575, 7576 }, cashShopPort: 7577);
        world.MigrateOut(1, 10, 0, MigrationSource.Login);
        world.MigrateIn(10, 0, out _);

        (IPEndPoint? shop, string? reason) = world.MigrateOut(1, 10, WorldState.CashShopChannel, MigrationSource.Channel);
        Assert.Null(reason);
        Assert.Equal(new IPEndPoint(Host, 7577), shop);
        Assert.True(world.MigrateIn(10, WorldState.CashShopChannel, out _).Ok);
        Assert.Equal(WorldState.CashShopChannel, world.ChannelOf(10));
        Assert.Equal(new IPEndPoint(Host, 7577), world.Channels().CashShop);

        // and back
        (IPEndPoint? back, _) = world.MigrateOut(1, 10, 0, MigrationSource.CashShop);
        Assert.Equal(new IPEndPoint(Host, 7575), back);
        Assert.True(world.MigrateIn(10, 0, out _).Ok);

        (WorldState noShop, _) = NewWorld();
        noShop.RegisterChannels("proc-a", Host, new[] { 7575 }, cashShopPort: null);
        (IPEndPoint? none, string? why) = noShop.MigrateOut(1, 10, WorldState.CashShopChannel, MigrationSource.Channel);
        Assert.Null(none);
        Assert.Contains("cash shop", why);
    }

    [Fact]
    public async Task LocalWorld_KeepsThePreSplitBehaviour_ForHandlersBuiltWithoutAWorld()
    {
        var endpoints = new[] { new IPEndPoint(IPAddress.Loopback, 7575), new IPEndPoint(IPAddress.Loopback, 7576) };
        var local = new LocalWorld(endpoints, cashShop: new IPEndPoint(IPAddress.Loopback, 7577));

        WorldView view = await local.GetWorldAsync();
        Assert.Equal(new[] { 0, 1 }, view.Channels.Select(c => c.Id));
        Assert.Equal(7576, view.Channels[1].Endpoint.Port);
        Assert.Equal(7577, view.CashShop?.Port);

        // Not strict: a character arriving without a hand-off (a test connecting straight to a
        // channel) is still admitted.
        Assert.True((await local.MigrateInAsync(42, 0)).Ok);
        Assert.Equal(endpoints[1], await local.MigrateOutAsync(1, 42, 1, MigrationSource.Channel));
        Assert.Null(await local.MigrateOutAsync(1, 42, 5, MigrationSource.Channel));

        byte[]? delivered = null;
        local.Broadcast = (packet, _) => { delivered = packet; return ValueTask.CompletedTask; };
        await local.BroadcastAsync(new byte[] { 1, 2, 3 });
        Assert.Equal(new byte[] { 1, 2, 3 }, delivered);
    }
}
