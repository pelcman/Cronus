using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The Maker lessons 6033 / 6036 at マレン: with the crafted crystal / gold anvil in hand the quest
/// completes and the job family's Maker skill (1007 / 10001007 / 20001007) is raised to 2 / 3.
/// </summary>
public class MakerLessonQuestTests
{
    private const int Maren = 2110004;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private sealed class Dialog : INpcDialog
    {
        private readonly BlockingCollection<int> _prompts = new();
        public bool TryTake(out int type, int ms) => _prompts.TryTake(out type, ms);
        public void Say(int npcId, string text, bool prev, bool next) => _prompts.Add(0);
        public void AskYesNo(int npcId, string text) => _prompts.Add(2);
        public void AskMenu(int npcId, string text) => _prompts.Add(5);
        public void AskText(int npcId, string text) => _prompts.Add(3);
        public void AskAccept(int npcId, string text) => _prompts.Add(13);
        public void AskAvatar(int npcId, string text, IReadOnlyList<int> styles) => _prompts.Add(8);
        public void OpenRps(int npcId) { }
    }

    private static NpcScriptEngine Engine()
    {
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scripts", "quest")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        return new NpcScriptEngine(
            new FolderNpcScriptSource(Path.Combine(root, "scripts", "npc")),
            new FolderNpcScriptSource(Path.Combine(root, "scripts", "quest")));
    }

    private static void Run(int quest, RecordingNpcPlayer player)
    {
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(quest, Maren, dialog, player, ending: true)!;
        while (!qm.IsEnded && !cts.IsCancellationRequested)
        {
            if (dialog.TryTake(out int type, 20))
            {
                qm.Advance(type, 1, -1, string.Empty);
            }
        }

        Assert.True(qm.IsEnded, "conversation did not end");
    }

    [Theory]
    [InlineData(112, 1007)]
    [InlineData(1111, 10001007)]
    [InlineData(2111, 20001007)]
    public void SecondLesson_RaisesTheFamilysMakerToTwo(int job, int makerSkill)
    {
        var player = new RecordingNpcPlayer { Job = job, Level = 80 };
        player.Skills[makerSkill] = 1;
        player.Inventory[4260003] = 1;
        Run(6033, player);
        Assert.Equal(new[] { 6033 }, player.CompletedQuests);
        Assert.Equal(2, player.Skills[makerSkill]);
        Assert.Equal(230000, player.Exp);
        Assert.Equal(1, player.Inventory[4260003]); // Cosmic keeps the crystal
    }

    [Fact]
    public void SecondLesson_WithoutTheCrystal_IsRefused()
    {
        var player = new RecordingNpcPlayer { Job = 112, Level = 80 };
        Run(6033, player);
        Assert.Empty(player.CompletedQuests);
        Assert.Empty(player.Skills);
    }

    [Fact]
    public void UnexpectedResult_TakesTheAnvil_AndRaisesMakerToThree()
    {
        var player = new RecordingNpcPlayer { Job = 412, Level = 110 };
        player.Skills[1007] = 2;
        player.Inventory[4031980] = 1;
        Run(6036, player);
        Assert.Equal(0, player.Inventory[4031980]);
        Assert.Equal(3, player.Skills[1007]);
        Assert.Equal(300000, player.Exp);
        Assert.Equal(new[] { 6036 }, player.CompletedQuests);

        var without = new RecordingNpcPlayer { Job = 412, Level = 110 };
        Run(6036, without);
        Assert.Empty(without.CompletedQuests);
    }
}
