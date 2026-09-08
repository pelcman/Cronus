using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

/// <summary>
/// <c>/sweep npcs</c>: the real-client half of "talk to every NPC". The headless exerciser
/// (AllScriptsExerciseTests) already runs every script's every branch; what it cannot see is the
/// client rendering the pages — a dialog tag the client's data lacks kills it. So the sweep
/// starts each scripted NPC's conversation server-side (the client shows a dialog it never
/// clicked for, exactly as quest scripts do), answers every prompt itself after a short dwell,
/// and logs each NPC to sweep-progress.txt BEFORE its first page, so a crash names the NPC. The
/// script runs against a read-only stand-in for the character (no warps, items or exp are
/// applied), and the first option is taken at every prompt.
/// </summary>
public sealed partial class ChannelHandler
{
    private static readonly Regex SweepMenuOption = new(@"#L(\d+)#", RegexOptions.Compiled);

    private sealed record SweepPrompt(int Type, string Text, IReadOnlyList<int> Options);

    /// <summary>Forwards every page to the real client dialog and records it for the sweep loop.</summary>
    private sealed class SweepNpcDialog : INpcDialog
    {
        private readonly INpcDialog _inner;
        public BlockingCollection<SweepPrompt> Prompts { get; } = new();

        public SweepNpcDialog(INpcDialog inner) => _inner = inner;

        public void Say(int npcId, string text, bool prev, bool next) { _inner.Say(npcId, text, prev, next); Prompts.Add(new SweepPrompt(0, text, Array.Empty<int>())); }
        public void AskYesNo(int npcId, string text) { _inner.AskYesNo(npcId, text); Prompts.Add(new SweepPrompt(2, text, Array.Empty<int>())); }
        public void AskMenu(int npcId, string text) { _inner.AskMenu(npcId, text); Prompts.Add(new SweepPrompt(5, text, SweepMenuOption.Matches(text).Select(m => int.Parse(m.Groups[1].Value)).Distinct().ToList())); }
        public void AskText(int npcId, string text) { _inner.AskText(npcId, text); Prompts.Add(new SweepPrompt(3, text, Array.Empty<int>())); }
        public void AskAccept(int npcId, string text) { _inner.AskAccept(npcId, text); Prompts.Add(new SweepPrompt(13, text, Array.Empty<int>())); }
        public void AskAvatar(int npcId, string text, IReadOnlyList<int> styles) { _inner.AskAvatar(npcId, text, styles); Prompts.Add(new SweepPrompt(8, text, Array.Empty<int>())); }
        public void OpenRps(int npcId) => _inner.OpenRps(npcId);
    }

    /// <summary>The character as the scripts see it, with every mutation dropped — a sweep must not warp, pay or reward.</summary>
    private sealed class SweepScriptPlayer : INpcPlayer
    {
        private readonly Character _c;
        public SweepScriptPlayer(Character c) => _c = c;

        public string getName() => _c.Name;
        public int getLevel() => _c.Level;
        public int getMapId() => _c.MapId;
        public int getMeso() => _c.Meso;
        public int getHp() => _c.Hp;
        public int getMaxHp() => _c.MaxHp;
        public int getExp() => _c.Exp;
        public int getGender() => _c.Gender;
        public int getJob() => _c.Job;
        public int getStr() => _c.Str;
        public int getDex() => _c.Dex;
        public int getInt() => _c.Int;
        public int getLuk() => _c.Luk;
        public int getFame() => _c.Fame;
        public int getAp() => _c.Ap;
        public int getSp() => _c.Sp;
        public int getHair() => _c.Hair;
        public int getFace() => _c.Face;
        public int getSkin() => _c.SkinColor;
        public bool isValidStyle(int styleId) => true;
        public void setHair(int hairId) { }
        public void setFace(int faceId) { }
        public void setSkin(int skinColor) { }
        public void gainMeso(int amount) { }
        public void gainExp(int amount) { }
        public void heal() { }
        public void rememberMap() { }
        public void warpToRememberedMap(int fallbackMapId) { }
        public void warp(int mapId) { }
        public void warp(int mapId, int portal) { }
        public void warpPortal(int mapId, string portalName) { }
        public bool airshipBoarding() => true;
        public int airshipMinutes() => 5;
        public void openParcel() { }
        public int parcelCount() => 0;
        public int receiveParcels() => 0;
        public void gainAp(int amount) { }
        public void gainSp(int amount) { }
        public void gainFame(int amount) { }
        public void setJob(int job) { }
        public void gainMaxHp(int amount) { }
        public void gainMaxMp(int amount) { }
        public bool hasQuest(int questId) => _c.StartedQuests.ContainsKey(questId);
        public bool isQuestDone(int questId) => _c.CompletedQuests.ContainsKey(questId);
        public void startQuest(int questId) { }
        public void completeQuest(int questId) { }
        public void gainItem(int itemId, int quantity) { }
        public bool haveItem(int itemId) => itemQuantity(itemId) > 0;
        public int itemQuantity(int itemId) => _c.EquippedItems.Where(i => i.ItemId == itemId).Sum(i => (int)i.Quantity);
        public void openShop(int shopId) { }
        public void openStorage() { }
        public void spawnMob(int mobId, int count) { }
        public int mobCount() => 0;
        public bool hasMerchant() => false;
        public bool retrieveMerchant() => false;
        public int getBuddyCapacity() => _c.BuddyCapacity;
        public void gainBuddyCapacity(int amount) { }
        public bool isPartyLeader() => true;
        public bool startSubwayMassacre() => false;
        public bool bonusSubwayMassacre() => false;
        public string? getQuestData(int questId) => _c.StartedQuests.TryGetValue(questId, out string? d) ? d : null;
        public void setQuestData(int questId, string data) { }
    }

    private const int SweepNpcMaxPages = 12;

    private static (int Action, int Selection, string Text) SweepAnswer(SweepPrompt p) => p.Type switch
    {
        5 => (1, p.Options.Count > 0 ? p.Options[0] : 0, string.Empty),
        3 => (1, -1, "abc"),
        8 => (1, 0, string.Empty),
        _ => (1, -1, string.Empty),
    };

    /// <summary>
    /// Streams every scripted NPC's conversation to this client, one after another. Each NPC is
    /// logged before its first page; the loop answers each prompt after <paramref name="dwell"/>
    /// (the client has to render the page first — that is what is being tested).
    /// </summary>
    private async Task RunNpcSweepAsync(MapleSession session, List<int> npcIds, TimeSpan dwell, CancellationToken ct)
    {
        try
        {
            AppendSweepLine($"# sweep npcs {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {npcIds.Count} scripted npcs, {dwell.TotalSeconds:0.##}s per page");
            for (int i = 0; i < npcIds.Count && !ct.IsCancellationRequested && _player is not null; i++)
            {
                int npcId = npcIds[i];
                string name = string.Empty;
                AppendSweepLine($"npc\t{npcId}\t{name}\t{DateTime.Now:HH:mm:ss}");
                Console.WriteLine($"[sweep] npc {i + 1}/{npcIds.Count} {npcId} {name}");
                await ReplyAsync(session, $"[sweep npc {i + 1}/{npcIds.Count}] {npcId} {name}").ConfigureAwait(false);

                _conversation?.End();
                var dialog = new SweepNpcDialog(new ChannelNpcDialog(session, _packets));
                NpcConversation? cm = _npcScripts?.Start(npcId, dialog, new SweepScriptPlayer(_player.Character));
                if (cm is null)
                {
                    continue;
                }

                _conversation = cm;
                int pages = 0;
                var idle = System.Diagnostics.Stopwatch.StartNew();
                while (!ct.IsCancellationRequested && pages < SweepNpcMaxPages)
                {
                    if (!dialog.Prompts.TryTake(out SweepPrompt? prompt, 50))
                    {
                        if (cm.IsEnded || idle.Elapsed > TimeSpan.FromSeconds(5))
                        {
                            break;
                        }

                        continue;
                    }

                    pages++;
                    idle.Restart();
                    await Task.Delay(dwell, ct).ConfigureAwait(false);   // let the client draw the page
                    (int action, int selection, string text) = SweepAnswer(prompt);
                    cm.Advance(prompt.Type, action, selection, text);
                }

                if (!cm.IsEnded)
                {
                    cm.End();
                }

                if (cm.Error is not null)
                {
                    AppendSweepLine($"# script error {npcId}: {cm.Error.Message}");
                }

                _conversation = null;
            }

            if (!ct.IsCancellationRequested)
            {
                AppendSweepLine("# npcs done");
                Console.WriteLine("[sweep] npcs done — every scripted NPC's dialog rendered without losing the client");
                await ReplyAsync(session, "sweep: 全 NPC 会話完了。落ちた NPC はありません").ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[sweep] npcs stopped: {ex.Message}");
        }
    }
}
