using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The 風来坊錬金術師 (Wandering Alchemist, NPC 2040050) crafting flow, driving the shipped
/// scripts/npc/2040050.js: picking "Magic Rock", recipe 0, and confirming consumes the exact
/// materials + 4000 meso and yields five 魔法の石 (4006000). Item ids are the JMS v186 ones.
/// </summary>
public class WanderingAlchemistTests
{
    private const int Npc = 2040050;
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
    {
        var engine = new NpcScriptEngine(new FolderNpcScriptSource(Path.Combine(RepoRoot(), "scripts", "npc")));
        return engine.Start(Npc, dialog, player)!;
    }

    [Fact]
    public void CraftingMagicRock_ConsumesTheRecipe_AndYieldsFive()
    {
        // Recipe 0 for Magic Rock: 20×4000046, 20×4000027, 1×4021001, +4000 meso → 5×4006000.
        var player = new RecordingNpcPlayer { Meso = 10_000 };
        foreach (var kv in new Dictionary<int,int>{[4000046]=30,[4000027]=25,[4021001]=2}) player.Inventory[kv.Key]=kv.Value;
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Start(dialog, player);

        Assert.Equal(0, dialog.Take(cts.Token).Type);                 // intro (sendNext)
        cm.Advance(0, 1, -1, string.Empty);
        Assert.Equal(5, dialog.Take(cts.Token).Type);                 // kind menu
        cm.Advance(5, 1, 0, string.Empty);                            // Magic Rock
        Assert.Equal(5, dialog.Take(cts.Token).Type);                 // recipe menu
        cm.Advance(5, 1, 0, string.Empty);                            // recipe 0
        Assert.Equal(2, dialog.Take(cts.Token).Type);                 // yes/no confirm
        cm.Advance(2, 1, -1, string.Empty);                           // yes
        Assert.Equal(0, dialog.Take(cts.Token).Type);                 // final ok
        cm.Advance(0, 1, -1, string.Empty);

        WaitUntilEnded(cm, cts.Token);
        Assert.Equal(10, player.Inventory.GetValueOrDefault(4000046));  // 30 - 20
        Assert.Equal(5, player.Inventory.GetValueOrDefault(4000027));   // 25 - 20
        Assert.Equal(1, player.Inventory.GetValueOrDefault(4021001));   // 2 - 1
        Assert.Equal(6_000, player.Meso);         // 10000 - 4000
        Assert.Equal(5, player.Inventory.GetValueOrDefault(4006000));   // five Magic Rocks
    }

    [Fact]
    public void WithoutTheMaterials_NothingIsConsumed()
    {
        var player = new RecordingNpcPlayer { Meso = 10_000 };
        player.Inventory[4000046] = 1;
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation cm = Start(dialog, player);

        Assert.Equal(0, dialog.Take(cts.Token).Type);
        cm.Advance(0, 1, -1, string.Empty);
        Assert.Equal(5, dialog.Take(cts.Token).Type);
        cm.Advance(5, 1, 0, string.Empty);
        Assert.Equal(5, dialog.Take(cts.Token).Type);
        cm.Advance(5, 1, 0, string.Empty);
        Assert.Equal(2, dialog.Take(cts.Token).Type);
        cm.Advance(2, 1, -1, string.Empty);       // yes, but lacks materials
        Assert.Equal(0, dialog.Take(cts.Token).Type);   // "check your materials" ok
        cm.Advance(0, 1, -1, string.Empty);

        WaitUntilEnded(cm, cts.Token);
        Assert.Equal(1, player.Inventory.GetValueOrDefault(4000046));   // untouched
        Assert.Equal(10_000, player.Meso);        // untouched
        Assert.Equal(0, player.Inventory.GetValueOrDefault(4006000));   // nothing made
    }

    private static void WaitUntilEnded(NpcConversation cm, CancellationToken ct)
    {
        while (!cm.IsEnded && !ct.IsCancellationRequested)
        {
            Thread.Sleep(10);
        }
    }
}
