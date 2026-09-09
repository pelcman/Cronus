using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// 村長タタモ (Tatamo, NPC 2081000): sells 魔法の種 (4031346) at 30,000 meso each, quantity picked
/// from a menu (the number-input dialog is not available yet — the menu carries a [DEV] note).
/// Drives the shipped script: buy 5 → −150,000 meso, +5 seeds; too poor → nothing changes.
/// </summary>
public class TatamoMagicSeedTests
{
    private const int Npc = 2081000;
    private const int Seed = 4031346;
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
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "npc", $"{Npc}.js")))
            {
                return dir.FullName;
            }
        }

        throw new FileNotFoundException($"scripts/npc/{Npc}.js not found");
    }

    private static NpcConversation Start(Dialog dialog, INpcPlayer player)
        => new NpcScriptEngine(new FolderNpcScriptSource(Path.Combine(RepoRoot(), "scripts", "npc"))).Start(Npc, dialog, player)!;

    private static void WaitEnded(NpcConversation cm, CancellationToken ct)
    {
        while (!cm.IsEnded && !ct.IsCancellationRequested)
        {
            Thread.Sleep(10);
        }
    }

    [Fact]
    public void BuyFiveSeeds_ChargesAndDelivers()
    {
        var player = new RecordingNpcPlayer { Meso = 200_000 };
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Start(dialog, player);

        Assert.Equal(5, dialog.Take(cts.Token).Type);           // main menu
        cm.Advance(5, 1, 0, string.Empty);                      // buy
        Assert.Equal(0, dialog.Take(cts.Token).Type);           // intro
        cm.Advance(0, 1, -1, string.Empty);
        Prompt amounts = dialog.Take(cts.Token);                // quantity menu
        Assert.Equal(5, amounts.Type);
        Assert.StartsWith(NpcConversation.DevPrefix.Trim(), amounts.Text); // simplified input is marked
        cm.Advance(5, 1, 1, string.Empty);                      // 5 個
        Assert.Equal(2, dialog.Take(cts.Token).Type);           // confirm
        cm.Advance(2, 1, -1, string.Empty);
        Assert.Equal(0, dialog.Take(cts.Token).Type);           // "またおいで"
        cm.Advance(0, 1, -1, string.Empty);

        WaitEnded(cm, cts.Token);
        Assert.Equal(50_000, player.Meso);
        Assert.Equal(5, player.Inventory.GetValueOrDefault(Seed));
    }

    [Fact]
    public void TooPoor_NothingChanges()
    {
        var player = new RecordingNpcPlayer { Meso = 10_000 };
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Start(dialog, player);

        cm_take(dialog, cts, 5); cm.Advance(5, 1, 0, string.Empty);
        cm_take(dialog, cts, 0); cm.Advance(0, 1, -1, string.Empty);
        cm_take(dialog, cts, 5); cm.Advance(5, 1, 0, string.Empty);   // 1 個 = 30,000 > 10,000
        cm_take(dialog, cts, 2); cm.Advance(2, 1, -1, string.Empty);
        cm_take(dialog, cts, 0); cm.Advance(0, 1, -1, string.Empty);   // "メソが足りない"

        WaitEnded(cm, cts.Token);
        Assert.Equal(10_000, player.Meso);
        Assert.Equal(0, player.Inventory.GetValueOrDefault(Seed));
    }

    [Fact]
    public void HelpLeafre_IsADevDecline()
    {
        var player = new RecordingNpcPlayer();
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Start(dialog, player);
        cm_take(dialog, cts, 5); cm.Advance(5, 1, 1, string.Empty);
        Prompt dev = dialog.Take(cts.Token);
        Assert.StartsWith(NpcConversation.DevPrefix, dev.Text);
        cm.Advance(0, 1, -1, string.Empty);
        WaitEnded(cm, cts.Token);
        Assert.True(cm.IsEnded);
    }

    private static void cm_take(Dialog d, CancellationTokenSource cts, int expectedType)
        => Assert.Equal(expectedType, d.Take(cts.Token).Type);
}
