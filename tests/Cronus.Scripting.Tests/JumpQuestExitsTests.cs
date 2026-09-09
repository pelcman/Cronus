using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// Jump-quest area helpers: the 魔女の塔 NextMap portal climbs one floor (seven JMS v186 maps, each
/// +100 target verified), and the 忍耐の森 statue 1061007 exits to Sleepywood on "yes".
/// </summary>
public class JumpQuestExitsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private static string RepoRoot()
    {
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scripts", "portal")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        return root;
    }

    [Theory]
    [InlineData(980041000, 980041100)]
    [InlineData(980041100, 980041200)]
    [InlineData(980042000, 980042100)]
    [InlineData(980042100, 980042200)]
    [InlineData(980043000, 980043100)]
    [InlineData(980043100, 980043200)]
    [InlineData(980044000, 980044100)]
    public void NextMap_ClimbsOneFloor(int floor, int above)
    {
        var player = new RecordingNpcPlayer { MapId = floor };
        new PortalScriptEngine(new FolderPortalScriptSource(Path.Combine(RepoRoot(), "scripts", "portal"))).Run("NextMap", player);
        Assert.Equal((above, 0), player.Warped);
    }

    [Fact]
    public void NextMap_DoesNothingOffTheTower()
    {
        var player = new RecordingNpcPlayer { MapId = 100000000 };
        new PortalScriptEngine(new FolderPortalScriptSource(Path.Combine(RepoRoot(), "scripts", "portal"))).Run("NextMap", player);
        Assert.Null(player.Warped);
    }

    private sealed class Dialog : INpcDialog
    {
        private readonly BlockingCollection<int> _prompts = new();
        public int Take(CancellationToken ct) => _prompts.Take(ct);
        public void Say(int npcId, string text, bool prev, bool next) => _prompts.Add(0);
        public void AskYesNo(int npcId, string text) => _prompts.Add(2);
        public void AskMenu(int npcId, string text) => _prompts.Add(5);
        public void AskText(int npcId, string text) => _prompts.Add(3);
        public void AskAccept(int npcId, string text) => _prompts.Add(13);
        public void AskAvatar(int npcId, string text, IReadOnlyList<int> styles) => _prompts.Add(8);
        public void OpenRps(int npcId) { }
    }

    [Theory]
    [InlineData(1, 105040300)] // yes → Sleepywood
    [InlineData(0, 0)]         // no  → stays
    public void CrumblingStatue_ExitsOnlyOnYes(int action, int expectedMap)
    {
        var player = new RecordingNpcPlayer { MapId = 105040310 };
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = new NpcScriptEngine(new FolderNpcScriptSource(Path.Combine(RepoRoot(), "scripts", "npc"))).Start(1061007, dialog, player)!;
        Assert.Equal(2, dialog.Take(cts.Token));
        cm.Advance(2, action, -1, string.Empty);
        while (!cm.IsEnded && !cts.Token.IsCancellationRequested)
        {
            Thread.Sleep(10);
        }

        if (expectedMap == 0)
        {
            Assert.Null(player.Warped);
        }
        else
        {
            Assert.Equal((expectedMap, 0), player.Warped);
        }
    }
}
