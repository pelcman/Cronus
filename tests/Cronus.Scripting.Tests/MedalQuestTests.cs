using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The title/medal quests: the adventurer titles (29900–29903, ダリア 9000040) announce the title on
/// their auto-start and hand the medal over when the player talks to ダリア; the Cygnus knight
/// titles (29906–29909, シグナス) and the Aran titles (29924–29928, ダリア 9000066) hand the medal
/// over on the spot and complete. Medal ids follow the JMS v186 Check data.
/// </summary>
public class MedalQuestTests
{
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

    private static void Run(int quest, int npc, RecordingNpcPlayer player, bool ending)
    {
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(quest, npc, dialog, player, ending)!;
        while (!qm.IsEnded && !cts.IsCancellationRequested)
        {
            if (dialog.TryTake(out int type, 20))
            {
                Assert.Equal(0, type);
                qm.Advance(0, 1, -1, string.Empty);
            }
        }

        Assert.True(qm.IsEnded, "conversation did not end");
    }

    public static IEnumerable<object[]> Adventurers() => new[]
    {
        new object[] { 29900, 1142107 },
        new object[] { 29901, 1142108 },
        new object[] { 29902, 1142109 },
        new object[] { 29903, 1142110 },
    };

    public static IEnumerable<object[]> OnTheSpot() => new[]
    {
        new object[] { 29906, 1101000, 1142066 },
        new object[] { 29907, 1101000, 1142067 },
        new object[] { 29908, 1101000, 1142068 },
        new object[] { 29909, 1101000, 1142069 },
        new object[] { 29924, 9000066, 1142129 },
        new object[] { 29925, 9000066, 1142130 },
        new object[] { 29926, 9000066, 1142131 },
        new object[] { 29927, 9000066, 1142132 },
        new object[] { 29928, 9000066, 1142133 },
    };

    [Theory]
    [MemberData(nameof(Adventurers))]
    public void AdventurerTitle_StartAnnounces_EndHandsTheMedalOver(int quest, int medal)
    {
        var player = new RecordingNpcPlayer();
        Run(quest, 9000040, player, ending: false);
        Assert.Equal(new[] { quest }, player.StartedQuests);
        Assert.Equal(0, player.Inventory.GetValueOrDefault(medal));

        Run(quest, 9000040, player, ending: true);
        Assert.Equal(1, player.Inventory[medal]);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
    }

    [Theory]
    [MemberData(nameof(Adventurers))]
    public void AdventurerTitle_AlreadyWearingTheMedal_IsNotGivenAnother(int quest, int medal)
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[medal] = 1;
        Run(quest, 9000040, player, ending: true);
        Assert.Equal(1, player.Inventory[medal]);
        Assert.Empty(player.CompletedQuests);
    }

    [Theory]
    [MemberData(nameof(OnTheSpot))]
    public void KnightAndAranTitles_HandTheMedalOverAndCompleteOnTheSpot(int quest, int npc, int medal)
    {
        var player = new RecordingNpcPlayer();
        Run(quest, npc, player, ending: false);
        Assert.Equal(1, player.Inventory[medal]);
        Assert.Equal(new[] { quest }, player.StartedQuests);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
    }

    [Theory]
    [MemberData(nameof(OnTheSpot))]
    public void KnightAndAranTitles_AlreadyWearingTheMedal_DoNothing(int quest, int npc, int medal)
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[medal] = 1;
        Run(quest, npc, player, ending: false);
        Assert.Equal(1, player.Inventory[medal]);
        Assert.Empty(player.CompletedQuests);
    }
}
