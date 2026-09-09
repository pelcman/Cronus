using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The Sharenian guild-quest area NPCs: the return monument 9040005 warps out to the excavation
/// base camp (a JMS v186 map), the three lore plaques only speak and end, and the monument of
/// honor 9040004 declines with a [DEV] notice (guild ranking display is not built yet).
/// </summary>
public class SharenianGuildQuestNpcsTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed record Prompt(int Type, string Text);

    private sealed class Dialog : INpcDialog
    {
        private readonly BlockingCollection<Prompt> _prompts = new();
        public Prompt Take(CancellationToken ct) => _prompts.Take(ct);
        public void Say(int npcId, string text, bool prev, bool next) => _prompts.Add(new Prompt(0, text));
        public void AskYesNo(int npcId, string text) => _prompts.Add(new Prompt(2, text));
        public void AskMenu(int npcId, string text) => _prompts.Add(new Prompt(5, text));
        public void AskText(int npcId, string text) => _prompts.Add(new Prompt(3, text));
        public void AskAccept(int npcId, string text) => _prompts.Add(new Prompt(13, text));
        public void AskAvatar(int npcId, string text, IReadOnlyList<int> styles) => _prompts.Add(new Prompt(8, text));
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
    public void ReturnMonument_Yes_WarpsToTheExcavationBaseCamp()
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(9040005, dialog, player)!;
        Assert.Equal(2, dialog.Take(cts.Token).Type);
        cm.Advance(2, 1, -1, string.Empty);
        WaitEnded(cm, cts.Token);
        Assert.Equal((101030104, 0), player.Warped);
    }

    [Fact]
    public void ReturnMonument_No_StaysPut()
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(9040005, dialog, player)!;
        Assert.Equal(2, dialog.Take(cts.Token).Type);
        cm.Advance(2, 0, -1, string.Empty);
        WaitEnded(cm, cts.Token);
        Assert.Null(player.Warped);
    }

    [Theory]
    [InlineData(9040007, "シャレン3世")]
    [InlineData(9040011, "ギルドクエスト")]
    [InlineData(9040012, "ロンギヌス")]
    public void LorePlaques_SpeakOnce_AndEnd(int npc, string mustContain)
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(npc, dialog, player)!;
        Prompt p = dialog.Take(cts.Token);
        Assert.Equal(0, p.Type);
        Assert.Contains(mustContain, p.Text);
        cm.Advance(0, 1, -1, string.Empty);
        WaitEnded(cm, cts.Token);
        Assert.True(cm.IsEnded);
        Assert.Null(player.Warped);
    }

    [Fact]
    public void MonumentOfHonor_DeclinesWithDevNotice()
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Engine().Start(9040004, dialog, player)!;
        Prompt p = dialog.Take(cts.Token);
        Assert.StartsWith(NpcConversation.DevPrefix, p.Text);
        cm.Advance(0, 1, -1, string.Empty);
        WaitEnded(cm, cts.Token);
        Assert.True(cm.IsEnded);
    }
}
