using System.Collections.Concurrent;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The Kerning City / desert talk quests shipped as scripts/quest files: the 沼地小屋 bin
/// (2214/2215: only 17–20 o'clock, 2215 also wants 2,000 meso), the four 情報 chats
/// (2216–2219: need the 4031894 note, pay 7,000 exp, the note is taken once all four are done),
/// リッチの感謝 2228, ゾンビキノコの信号体系3 2251 (20 × 4032399), and the ラクダ talks
/// 2257 / 2259 (must have moved on from 260020000) / 2260 (needs a 2nd job).
/// </summary>
public class KerningInfoAndCamelQuestTests
{
    private const int Bin = 1052108;
    private const int Camel = 2110005;
    private const int Note = 4031894;
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

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

    /// <summary>Runs one quest entry point, answering every plain dialog line with "next" until it ends.</summary>
    private static void Run(int quest, int npc, RecordingNpcPlayer player, bool ending)
    {
        var dialog = new Dialog();
        using var cts = new CancellationTokenSource(Timeout);
        NpcConversation qm = Engine().StartQuest(quest, npc, dialog, player, ending)!;
        while (!qm.IsEnded && !cts.IsCancellationRequested)
        {
            int type;
            try
            {
                type = dialog.Take(cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            Assert.Equal(0, type);
            qm.Advance(0, 1, -1, string.Empty);
        }

        Assert.True(qm.IsEnded, "conversation did not end");
    }

    [Theory]
    [InlineData(2214)]
    [InlineData(2215)]
    public void SwampHut_OutsideTheEvening_IsRefused(int quest)
    {
        var player = new RecordingNpcPlayer { Hour = 10, Meso = 10_000 };
        Run(quest, Bin, player, ending: true);
        Assert.Empty(player.CompletedQuests);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(19)]
    public void SwampHut_InTheEvening_Completes(int hour)
    {
        var player = new RecordingNpcPlayer { Hour = hour };
        Run(2214, Bin, player, ending: true);
        Assert.Equal(new[] { 2214 }, player.CompletedQuests);
    }

    [Fact]
    public void SwampHut_AtEight_IsAlreadyTooLate()
    {
        var player = new RecordingNpcPlayer { Hour = 20 };
        Run(2214, Bin, player, ending: true);
        Assert.Empty(player.CompletedQuests);
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(1999, false)]
    public void SwampHutAgain_NeedsTwoThousandMeso(int meso, bool completes)
    {
        var player = new RecordingNpcPlayer { Hour = 18, Meso = meso };
        Run(2215, Bin, player, ending: true);
        Assert.Equal(completes ? new[] { 2215 } : Array.Empty<int>(), player.CompletedQuests);
        Assert.Equal(meso, player.Meso); // the 2,000 is taken by the quest Act, not the script
    }

    [Theory]
    [InlineData(2216, 9000008)]
    [InlineData(2217, 1052102)]
    [InlineData(2218, 1052103)]
    [InlineData(2219, 1052006)]
    public void Info_CompletesAndPaysExp_KeepingTheNoteWhileOthersRemain(int quest, int npc)
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[Note] = 1;
        Run(quest, npc, player, ending: false);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
        Assert.Equal(7000, player.Exp);
        Assert.Equal(1, player.Inventory[Note]);
    }

    [Fact]
    public void Info_LastOfTheFour_TakesTheNote()
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[Note] = 1;
        player.SetQuestDone(2216, 2217, 2218);
        Run(2219, 1052006, player, ending: false);
        Assert.Equal(new[] { 2219 }, player.CompletedQuests);
        Assert.Equal(0, player.Inventory[Note]);
    }

    [Fact]
    public void RichsThanks_OneLine_ThenCompletes()
    {
        var player = new RecordingNpcPlayer();
        Run(2228, 1032108, player, ending: false);
        Assert.Equal(new[] { 2228 }, player.CompletedQuests);
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(19, false)]
    public void ZombieMushroomSignals_NeedTwentyTalismans(int have, bool completes)
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[4032399] = have;
        Run(2251, 1061011, player, ending: true);
        Assert.Equal(completes ? new[] { 2251 } : Array.Empty<int>(), player.CompletedQuests);
        Assert.Equal(completes ? 0 : have, player.Inventory[4032399]);
    }

    [Fact]
    public void CartasasConfession_CompletesAtTheCamel()
    {
        var player = new RecordingNpcPlayer();
        Run(2257, Camel, player, ending: true);
        Assert.Equal(new[] { 2257 }, player.CompletedQuests);
    }

    [Fact]
    public void CamelNightTalk_Start_ThenEnd_OnlyAwayFromTheOasis()
    {
        var player = new RecordingNpcPlayer { MapId = 260020000 };
        Run(2259, Camel, player, ending: false);
        Assert.Equal(new[] { 2259 }, player.StartedQuests);

        Run(2259, Camel, player, ending: true);
        Assert.Empty(player.CompletedQuests);

        player.MapId = 260020700;
        Run(2259, Camel, player, ending: true);
        Assert.Equal(new[] { 2259 }, player.CompletedQuests);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(100, false)]
    [InlineData(111, true)]
    [InlineData(211, true)]
    [InlineData(1100, false)]
    [InlineData(1111, true)]
    public void TowardsMushroomCastle_EndsOnlyWithASecondJob(int job, bool completes)
    {
        var player = new RecordingNpcPlayer { Job = job };
        Run(2260, Camel, player, ending: false);
        Assert.Equal(new[] { 2260 }, player.StartedQuests);

        Run(2260, Camel, player, ending: true);
        Assert.Equal(completes ? new[] { 2260 } : Array.Empty<int>(), player.CompletedQuests);
    }
}
