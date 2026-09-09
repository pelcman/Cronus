using System.Linq;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The タイムロード (Road of Time) gate portal, scripts/portal/timeQuest.js: on each "道" map it
/// advances the player when the lane's quest is done and bounces to a safe lane otherwise. The
/// forward map ids and the gate quest ids are those of the JMS v186 client (verified 1:1 against
/// the client's portal graph), so this pins the whole ladder.
/// </summary>
public class TimeQuestPortalTests
{
    private static RecordingNpcPlayer Player(int map, IEnumerable<int> questsDone, IEnumerable<int>? items = null)
    {
        var p = new RecordingNpcPlayer { MapId = map };
        p.SetQuestDone(questsDone.ToArray());
        foreach (int i in items ?? Array.Empty<int>()) p.Inventory[i] = 1;
        return p;
    }

    private static PortalScriptEngine Engine()
    {
        string root = RepoRoot();
        return new PortalScriptEngine(new FolderPortalScriptSource(Path.Combine(root, "scripts", "portal")));
    }

    private static string RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "scripts", "portal")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("scripts/portal not found");
    }

    [Theory]
    // zone 1 (思い出の道): lane N gates quest 3500+N, advances +10; lane 5 → next zone
    [InlineData(270010100, 3501, 270010110)]
    [InlineData(270010200, 3502, 270010210)]
    [InlineData(270010400, 3504, 270010410)]
    [InlineData(270010500, 3507, 270020000)]
    // zone 2 (後悔の道): map = 101..105
    [InlineData(270020100, 3508, 270020110)]
    [InlineData(270020500, 3514, 270030000)]
    // zone 3 (忘却の道): map = 201..205
    [InlineData(270030100, 3515, 270030110)]
    [InlineData(270030500, 3519, 270040000)]
    // temple corridor
    [InlineData(270040000, 3522, 270040100)]
    public void WithTheGateQuestDone_ItAdvancesToTheNextLane(int map, int gateQuest, int forward)
    {
        var player = Player(map, new[] { gateQuest });
        Engine().Run("timeQuest", player);
        Assert.Equal((forward, "out00"), player.WarpedNamed);
    }

    [Theory]
    [InlineData(270010100, 270010000)] // zone 1 → its entry
    [InlineData(270020300, 270020000)] // zone 2 → its entry
    [InlineData(270030300, 270030000)] // zone 3 → its entry
    [InlineData(270040000, 270030000)] // temple corridor, gate not met → zone 3 entry
    public void WithoutTheGateQuest_ItBouncesToTheZoneSafeLane(int map, int safeLane)
    {
        var player = Player(map, Array.Empty<int>());
        Engine().Run("timeQuest", player);
        Assert.Equal((safeLane, "in00"), player.WarpedNamed);
    }

    [Theory]
    [InlineData(100000000)] // a town: the portal does not exist here
    [InlineData(270040100)] // a warp target, not a lane map
    public void OffTheRoadOfTime_ItDoesNothing_EvenWithEveryQuestDone(int map)
    {
        var player = Player(map, Enumerable.Range(3400, 200));
        Engine().Run("timeQuest", player);
        Assert.Null(player.WarpedNamed);
    }

    [Fact]
    public void TheTempleCorridorAlsoOpensWithThePass_4032002()
    {
        var player = Player(270040000, Array.Empty<int>(), items: new[] { 4032002 });
        Engine().Run("timeQuest", player);
        Assert.Equal((270040100, "out00"), player.WarpedNamed);
    }
}
