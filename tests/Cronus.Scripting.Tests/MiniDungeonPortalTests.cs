using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The eleven MD_* portals: from the entrance map they ask the channel for the dungeon (a [DEV]
/// notice when it is held by strangers); from inside they warp back to the entrance's MD00 portal.
/// Dungeon ids are the JMS v186 real maps (the one with mobs — e.g. 240020501, not Cosmic's 240020512).
/// </summary>
public class MiniDungeonPortalTests
{
    private static PortalScriptEngine Engine()
    {
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scripts", "portal")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        return new PortalScriptEngine(new FolderPortalScriptSource(Path.Combine(root, "scripts", "portal")));
    }

    public static IEnumerable<object[]> Dungeons() => new[]
    {
        new object[] { "MD_pig", 100020000, 100020100 },
        new object[] { "MD_mushroom", 105050100, 105050101 },
        new object[] { "MD_golem", 105040304, 105040320 },
        new object[] { "MD_drakeroom", 105090311, 105090320 },
        new object[] { "MD_protect", 240040520, 240040900 },
        new object[] { "MD_rabbit", 221023400, 221023401 },
        new object[] { "MD_remember", 240040511, 240040800 },
        new object[] { "MD_roundTable", 240020500, 240020501 },
        new object[] { "MD_sand", 260020600, 260020630 },
        new object[] { "MD_treasure", 251010402, 251010410 },
        new object[] { "MD_error", 261020300, 261020301 },
    };

    [Theory]
    [MemberData(nameof(Dungeons))]
    public void FromTheEntrance_EntersTheDungeon(string script, int entrance, int dungeon)
    {
        var player = new RecordingNpcPlayer { MapId = entrance };
        Engine().Run(script, player);
        Assert.Equal(dungeon, player.EnteredDungeon);
        Assert.Empty(player.Messages);
    }

    [Theory]
    [MemberData(nameof(Dungeons))]
    public void FromTheEntrance_WhenHeld_SaysSoAndStays(string script, int entrance, int dungeon)
    {
        _ = dungeon;
        var player = new RecordingNpcPlayer { MapId = entrance, MiniDungeonFree = false };
        Engine().Run(script, player);
        Assert.Null(player.EnteredDungeon);
        Assert.Null(player.WarpedNamed);
        Assert.Single(player.Messages);
        Assert.StartsWith("[DEV]", player.Messages[0]);
    }

    [Theory]
    [MemberData(nameof(Dungeons))]
    public void FromInside_LeavesToTheEntrancePortal(string script, int entrance, int dungeon)
    {
        var player = new RecordingNpcPlayer { MapId = dungeon };
        Engine().Run(script, player);
        Assert.Equal((entrance, "MD00"), player.WarpedNamed);
        Assert.Null(player.EnteredDungeon);
    }
}
