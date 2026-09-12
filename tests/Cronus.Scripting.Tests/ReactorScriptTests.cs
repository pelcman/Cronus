using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The reactor scripts with side effects (drops are data-driven and need no script): ruin warps,
/// mob-spawning statues / chests / altars, the mine and doll-house traps, the toy factory's
/// secret passages. Spawns land at the breaker's feet through <c>player.spawnMob</c>.
/// </summary>
public class ReactorScriptTests
{
    private static PortalScriptEngine Engine()
    {
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scripts", "reactor")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        return new PortalScriptEngine(new FolderPortalScriptSource(Path.Combine(root, "scripts", "reactor")));
    }

    private sealed class SpawnRecorder : RecordingNpcPlayer
    {
    }

    [Theory]
    [InlineData("1020000", "pt00")]
    [InlineData("1020001", "pt01")]
    [InlineData("1020002", "pt02")]
    public void RuinDevices_WarpWithinTheRuin(string reactor, string portal)
    {
        var player = new RecordingNpcPlayer { MapId = 910200000 };
        Engine().Run(reactor, player);
        Assert.Equal((910200000, portal), player.WarpedNamed);
    }

    [Theory]
    [InlineData("2110000", 280010000)]
    [InlineData("2200000", 221023200)]
    public void Traps_SendYouBack_WithANotice(string reactor, int map)
    {
        var player = new RecordingNpcPlayer();
        Engine().Run(reactor, player);
        Assert.Equal((map, 0), player.Warped);
        Assert.Single(player.Messages);
    }

    [Fact]
    public void SecretFactory_GoesToOneOfTheTwoLines()
    {
        var player = new RecordingNpcPlayer();
        Engine().Run("2200001", player);
        Assert.NotNull(player.Warped);
        Assert.Contains(player.Warped!.Value.Map, new[] { 922000020, 922000021 });
    }

    [Fact]
    public void ToyFactoryDevice_FollowsTheGhostQuest()
    {
        var onQuest = new RecordingNpcPlayer();
        onQuest.StartedQuests.Add(3238);
        Engine().Run("2202002", onQuest);
        Assert.Equal((922000020, 0), onQuest.Warped);

        var other = new RecordingNpcPlayer();
        Engine().Run("2202002", other);
        Assert.Equal((922000009, 0), other.Warped);
    }

    [Fact]
    public void ForgottenShrine_MostlyWarpsOut()
    {
        int warps = 0;
        for (int i = 0; i < 200; i++)
        {
            var player = new RecordingNpcPlayer { MapId = 910500200 };
            Engine().Run("1050000", player);
            if (player.Warped is { Map: 105090200 })
            {
                warps++;
            }
        }

        Assert.InRange(warps, 100, 190);
    }
}
