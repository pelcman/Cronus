using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cronus.Scripting;
using Xunit;

namespace Cronus.Scripting.Tests;

/// <summary>
/// The Temple of Time memory quests (3523–3539: flag record 7081 for 思い出の観照者 3507 and
/// complete themselves), the singles 3108 / 3714 / 3953, the Magatia Dran chain (3320 / 3353
/// warp into the lab, 3321 / 3354 start, Phaewen and Dran NPC scripts), and the secret door:
/// 3360 hands out a 10-character key that the door NPC 2111024 checks before crossing to
/// 261030000; the secretDoor portal skips the prompt once authenticated.
/// </summary>
public class TimeMemoriesAndMagatiaDranTests
{
    private const int Door = 2111024;
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

    private static string RepoRoot()
    {
        string root = AppContext.BaseDirectory;
        while (!Directory.Exists(Path.Combine(root, "scripts", "quest")))
        {
            root = Directory.GetParent(root)!.FullName;
        }

        return root;
    }

    private static NpcScriptEngine Engine() => new(
        new FolderNpcScriptSource(Path.Combine(RepoRoot(), "scripts", "npc")),
        new FolderNpcScriptSource(Path.Combine(RepoRoot(), "scripts", "quest")));

    private static PortalScriptEngine Portals()
        => new(new FolderPortalScriptSource(Path.Combine(RepoRoot(), "scripts", "portal")));

    private static void Drive(NpcConversation cm, Dialog dialog, int yes = 1, int accept = 1, string text = "")
    {
        using var cts = new CancellationTokenSource(Timeout);
        while (!cm.IsEnded && !cts.IsCancellationRequested)
        {
            if (dialog.TryTake(out int type, 20))
            {
                cm.Advance(type, type switch { 2 => yes, 13 => accept, _ => 1 }, -1, type == 3 ? text : string.Empty);
            }
        }

        Assert.True(cm.IsEnded, "conversation did not end");
    }

    private static void RunQuest(int quest, int npc, RecordingNpcPlayer player, bool ending, int yes = 1, int accept = 1)
    {
        var dialog = new Dialog();
        Drive(Engine().StartQuest(quest, npc, dialog, player, ending)!, dialog, yes, accept);
    }

    private static void RunNpc(int npc, RecordingNpcPlayer player, int yes = 1, string text = "")
    {
        var dialog = new Dialog();
        Drive(Engine().Start(npc, dialog, player)!, dialog, yes, 1, text);
    }

    [Theory]
    [InlineData(3523, 1022000)]
    [InlineData(3524, 1032001)]
    [InlineData(3525, 1012100)]
    [InlineData(3526, 1052001)]
    [InlineData(3527, 1090000)]
    [InlineData(3529, 1101002)]
    [InlineData(3539, 1201000)]
    public void LostMemory_FlagsTheWatcherRecord_AndCompletesItself(int quest, int npc)
    {
        var player = new RecordingNpcPlayer();
        player.StartedQuests.Add(3507);
        RunQuest(quest, npc, player, ending: false);
        Assert.Equal("1", player.QuestData[7081]);
        Assert.Contains(quest, player.StartedQuests);
        Assert.Equal(new[] { quest }, player.CompletedQuests);
    }

    [Fact]
    public void SnowmanClue_CompletesOnTheSpot()
    {
        var player = new RecordingNpcPlayer();
        RunQuest(3108, 2020012, player, ending: false);
        Assert.Equal(new[] { 3108 }, player.CompletedQuests);
    }

    [Fact]
    public void NineSpiritEgg_ReturnedForTheHeart()
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[4001094] = 1;
        RunQuest(3714, 2081011, player, ending: false);
        Assert.Equal(0, player.Inventory[4001094]);
        Assert.Equal(1, player.Inventory[2041200]);
        Assert.Equal(42000, player.Exp);
        Assert.Equal(new[] { 3714 }, player.CompletedQuests);

        var without = new RecordingNpcPlayer();
        RunQuest(3714, 2081011, without, ending: false);
        Assert.Empty(without.CompletedQuests);
    }

    [Fact]
    public void Muhammad_TakesTheLithium_AndCompletes()
    {
        var player = new RecordingNpcPlayer();
        player.Inventory[4011008] = 1;
        RunQuest(3953, 2100001, player, ending: true);
        Assert.Equal(0, player.Inventory[4011008]);
        Assert.Equal(20000, player.Exp);
        Assert.Equal(new[] { 3953 }, player.CompletedQuests);

        var without = new RecordingNpcPlayer();
        RunQuest(3953, 2100001, without, ending: true);
        Assert.Empty(without.CompletedQuests);
    }

    [Theory]
    [InlineData(3320)]
    [InlineData(3353)]
    public void PhaewenSendsYouToDransLab_OnAccept(int quest)
    {
        var yes = new RecordingNpcPlayer();
        RunQuest(quest, 2111006, yes, ending: false, accept: 1);
        Assert.Equal(new[] { quest }, yes.StartedQuests);
        Assert.Equal((926120200, 0), yes.Warped);

        var no = new RecordingNpcPlayer();
        RunQuest(quest, 2111006, no, ending: false, accept: 0);
        Assert.Empty(no.StartedQuests);
        Assert.Null(no.Warped);
    }

    [Theory]
    [InlineData(3321)]
    [InlineData(3354)]
    public void DranQuests_StartOnAccept(int quest)
    {
        var yes = new RecordingNpcPlayer();
        RunQuest(quest, 2111002, yes, ending: false, accept: 1);
        Assert.Equal(new[] { quest }, yes.StartedQuests);

        var no = new RecordingNpcPlayer();
        RunQuest(quest, 2111002, no, ending: false, accept: 0);
        Assert.Empty(no.StartedQuests);
    }

    [Fact]
    public void PhaewenNpc_WarpsToTheLab_OnlyOnceTheQuestIsKnown()
    {
        var known = new RecordingNpcPlayer();
        known.StartedQuests.Add(3320);
        RunNpc(2111006, known, yes: 1);
        Assert.Equal((926120200, 0), known.Warped);

        var stranger = new RecordingNpcPlayer();
        RunNpc(2111006, stranger, yes: 1);
        Assert.Null(stranger.Warped);
    }

    [Fact]
    public void DranNpc_OffersTheWayOut()
    {
        var player = new RecordingNpcPlayer { MapId = 926120200 };
        RunNpc(2111002, player, yes: 1);
        Assert.Equal((261020401, 0), player.Warped);

        var stay = new RecordingNpcPlayer { MapId = 926120200 };
        RunNpc(2111002, stay, yes: 0);
        Assert.Null(stay.Warped);
    }

    [Fact]
    public void MasterKey_IsHandedOut_AndStoredOnTheRecord()
    {
        var player = new RecordingNpcPlayer();
        RunQuest(3360, 2111006, player, ending: false, accept: 1);
        Assert.Equal(new[] { 3360 }, player.StartedQuests);
        Assert.Matches(new Regex("^[0-9A-Z]{10}$"), player.QuestData[3360]);

        var declined = new RecordingNpcPlayer();
        RunQuest(3360, 2111006, declined, ending: false, accept: 0);
        Assert.Empty(declined.StartedQuests);
        Assert.Empty(declined.QuestData);
    }

    [Theory]
    [InlineData(261010000, "sp_jenu")]
    [InlineData(261020200, "sp_alca")]
    public void SecretDoor_RightKey_Authenticates_AndCrosses(int map, string back)
    {
        var player = new RecordingNpcPlayer { MapId = map };
        player.StartedQuests.Add(3360);
        player.QuestData[3360] = "A1B2C3D4E5";
        RunNpc(Door, player, text: "A1B2C3D4E5");
        Assert.Equal("1", player.QuestData[3360]);
        Assert.Equal((261030000, back), player.WarpedNamed);
    }

    [Fact]
    public void SecretDoor_WrongKey_StaysShut()
    {
        var player = new RecordingNpcPlayer { MapId = 261010000 };
        player.StartedQuests.Add(3360);
        player.QuestData[3360] = "A1B2C3D4E5";
        RunNpc(Door, player, text: "WRONG");
        Assert.Equal("A1B2C3D4E5", player.QuestData[3360]);
        Assert.Null(player.WarpedNamed);
    }

    [Fact]
    public void SecretDoor_WithoutTheQuest_IsLocked()
    {
        var player = new RecordingNpcPlayer { MapId = 261010000 };
        RunNpc(Door, player, text: "ANYTHING");
        Assert.Null(player.WarpedNamed);
        Assert.Empty(player.QuestData);
    }

    [Fact]
    public void SecretDoor_OnceAuthenticatedOrDone_CrossesWithoutAsking()
    {
        var authenticated = new RecordingNpcPlayer { MapId = 261020200 };
        authenticated.StartedQuests.Add(3360);
        authenticated.QuestData[3360] = "1";
        RunNpc(Door, authenticated);
        Assert.Equal((261030000, "sp_alca"), authenticated.WarpedNamed);

        var done = new RecordingNpcPlayer { MapId = 261010000 };
        done.SetQuestDone(3360);
        Portals().Run("secretDoor", done);
        Assert.Equal((261030000, "sp_jenu"), done.WarpedNamed);
        Assert.Null(done.OpenedNpc);
    }

    [Fact]
    public void SecretDoorPortal_BeforeAuthentication_OpensTheDoorNpc()
    {
        var player = new RecordingNpcPlayer { MapId = 261010000 };
        player.StartedQuests.Add(3360);
        player.QuestData[3360] = "A1B2C3D4E5";
        Portals().Run("secretDoor", player);
        Assert.Equal(Door, player.OpenedNpc);
        Assert.Null(player.WarpedNamed);
    }
}
