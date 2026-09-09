using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The transport / exit NPCs (2007, 1013001, 1063016, 2022004, 2133002): each sends the player to
/// a JMS v186 map that was verified to exist. These drive the shipped scripts and assert the warp
/// (and, for the Ellin Forest signpost, that the instance items are cleared on the way out).
/// </summary>
public class TransportNpcsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed record Prompt(int Type);

    private sealed class Dialog : INpcDialog
    {
        private readonly BlockingCollection<Prompt> _prompts = new();
        public Prompt Take(CancellationToken ct) => _prompts.Take(ct);
        public void Say(int npcId, string text, bool prev, bool next) => _prompts.Add(new Prompt(0));
        public void AskYesNo(int npcId, string text) => _prompts.Add(new Prompt(2));
        public void AskMenu(int npcId, string text) => _prompts.Add(new Prompt(5));
        public void AskText(int npcId, string text) => _prompts.Add(new Prompt(3));
        public void AskAccept(int npcId, string text) => _prompts.Add(new Prompt(13));
        public void AskAvatar(int npcId, string text, IReadOnlyList<int> styles) => _prompts.Add(new Prompt(8));
        public void OpenRps(int npcId) { }
    }

    private static string RepoRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "scripts", "npc")))
            {
                return dir.FullName;
            }
        }

        throw new DirectoryNotFoundException("scripts/npc not found");
    }

    private static NpcScriptEngine Engine()
        => new(new FolderNpcScriptSource(Path.Combine(RepoRoot(), "scripts", "npc")));

    private static void WaitEnded(NpcConversation cm, CancellationToken ct)
    {
        while (!cm.IsEnded && !ct.IsCancellationRequested)
        {
            Thread.Sleep(10);
        }
    }

    [Fact]
    public void EventGuide_Yes_WarpsToLithHarbor()
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(2007, dialog, player)!;
        Assert.Equal(2, dialog.Take(cts.Token).Type);   // ask yes/no
        cm.Advance(2, 1, -1, string.Empty);             // yes
        WaitEnded(cm, cts.Token);
        Assert.Equal((104000000, 0), player.Warped);
    }

    [Fact]
    public void MysteriousStatue_Yes_WarpsOutOfTheTrial()
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(1063016, dialog, player)!;
        Assert.Equal(2, dialog.Take(cts.Token).Type);
        cm.Advance(2, 1, -1, string.Empty);
        WaitEnded(cm, cts.Token);
        Assert.Equal((105040201, 2), player.Warped);
    }

    [Fact]
    public void Tylus_WarpsToElNath_NamedPortal()
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(2022004, dialog, player)!;
        Assert.Equal(0, dialog.Take(cts.Token).Type);   // sendNext
        cm.Advance(0, 1, -1, string.Empty);
        WaitEnded(cm, cts.Token);
        Assert.Equal((211000000, "in01"), player.WarpedNamed);
    }

    [Fact]
    public void EllinForestSignpost_Yes_ClearsInstanceItems_AndWarpsToTheExit()
    {
        var player = new RecordingNpcPlayer();
        foreach (var kv in new Dictionary<int,int>{[4001163]=3,[4001169]=1,[2270004]=5,[4000000]=9}) player.Inventory[kv.Key]=kv.Value;
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(2133002, dialog, player)!;
        Assert.Equal(2, dialog.Take(cts.Token).Type);
        cm.Advance(2, 1, -1, string.Empty);
        WaitEnded(cm, cts.Token);
        Assert.Equal((930000800, 0), player.Warped);
        Assert.Equal(0, player.Inventory.GetValueOrDefault(4001163));
        Assert.Equal(0, player.Inventory.GetValueOrDefault(4001169));
        Assert.Equal(0, player.Inventory.GetValueOrDefault(2270004));
        Assert.Equal(9, player.Inventory.GetValueOrDefault(4000000)); // an unrelated item is left alone
    }

    [Fact]
    public void DragonContract_WarpsToTheDreamMap()
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(1013001, dialog, player)!;
        Assert.Equal(0, dialog.Take(cts.Token).Type);
        cm.Advance(0, 1, -1, string.Empty);
        Assert.Equal(0, dialog.Take(cts.Token).Type);
        cm.Advance(0, 1, -1, string.Empty);
        WaitEnded(cm, cts.Token);
        Assert.Equal((900090101, 0), player.Warped);
    }
}
