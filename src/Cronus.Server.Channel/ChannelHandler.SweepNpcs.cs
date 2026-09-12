using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

/// <summary>
/// <c>/sweep npcs</c> and <c>/sweep quests</c>: the real-client half of "open every script". The headless exerciser
/// (AllScriptsExerciseTests) already runs every script's every branch; what it cannot see is the
/// client rendering the pages — a dialog tag the client's data lacks kills it. So the sweep
/// starts each scripted NPC's conversation server-side (the client shows a dialog it never
/// clicked for, exactly as quest scripts do), lets the client draw the first page for a moment,
/// and logs each NPC to sweep-progress.txt BEFORE its page, so a crash names the NPC. The script
/// runs against a read-only stand-in for the character (no warps, items or exp are applied).
/// Only the first page: the client disconnects when a second script message arrives over an
/// unanswered dialog, and only the client can answer one.
/// </summary>
public sealed partial class ChannelHandler
{
    private static readonly Regex SweepMenuOption = new(@"#L(\d+)#", RegexOptions.Compiled);

    private sealed record SweepPrompt(int Type, string Text, IReadOnlyList<int> Options);

    /// <summary>The NPC whose page is on the client right now (for the crash marker), 0 between NPCs.</summary>
    private int _sweepCurrentNpc;

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
        public void setHp(int hp) { }
        public void rememberMap() { }
        public void warpToRememberedMap(int fallbackMapId) { }
        public void warp(int mapId, int portal = 0) { }
        public void warpPortal(int mapId, string portalName) { }
        public bool airshipBoarding() => true;
        public int airshipMinutes() => 5;
        public void openParcel() { }
        public int parcelCount() => 0;
        public int receiveParcels() => 0;
        public void gainAp(int amount) { }
        public void gainSp(int amount) { }
        public void gainFame(int amount) { }
        public void changeJob(int job) => setJob(job);
        public void resetStatsForJob() { }
        public int getSkillLevel(int skillId) => _c.Skills.GetValueOrDefault(skillId);
        public void teachSkill(int skillId, int level) { }
        public void message(string text) { }
        public bool enterMiniDungeon(int dungeonMapId) => false;
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
        public int dojoPoints() => 0;
        public void setDojoPoints(int points) { }
        public bool dojoEnter(bool party, int fromStage) => false;
        public void dojoNextStage() { }
        public bool dojoTeleportUp() => false;
        public void dojoExit() { }
        public bool dojoTutorialExit() => false;
        public void openNpc(int npcId) { }
        public int hourOfDay() => 12;
        public void showScreenEffect(string path) { }
    }

    /// <summary>
    /// Shows every scripted NPC's first dialog page on this client, one after another, logging
    /// each NPC before its page and closing the dialog with a same-map SetField between NPCs.
    /// </summary>
    private async Task RunNpcSweepAsync(MapleSession session, List<int> npcIds, TimeSpan dwell, CancellationToken ct)
    {
        try
        {
            AppendSweepLine($"# sweep npcs {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {npcIds.Count} scripted npcs, {dwell.TotalSeconds:0.##}s per page");
            for (int i = 0; i < npcIds.Count && !ct.IsCancellationRequested && _player is not null; i++)
            {
                int npcId = npcIds[i];
                string name = _npcNames?.GetName(npcId) ?? string.Empty;
                if (_npcNames is not null && !_npcNames.HasImage(npcId))
                {
                    // No portrait in this client's Npc.wz: the dialog would crash it (0x80030002).
                    AppendSweepLine($"# skip {npcId} {name}: no client image (Npc.wz) — the script targets an id this client does not draw");
                    Console.WriteLine($"[sweep] npc {i + 1}/{npcIds.Count} {npcId} skipped: no client image");
                    continue;
                }

                _sweepCurrentNpc = npcId;
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

                // First page only. The client closes its socket when a new script message arrives
                // while its dialog is still open and unanswered (2026-09-09: the first version of
                // this sweep sent NPC 2's page over NPC 1's and was thrown back to the login
                // screen), and only the client can answer a page. So: show the page, let the
                // client draw it for `dwell`, end the script server-side, and close the dialog the
                // one way the server can — a SetField onto the same map. Deeper pages are covered
                // headless by AllScriptsExerciseTests; the crash harness (keyboard) can walk them.
                _conversation = cm;
                var idle = System.Diagnostics.Stopwatch.StartNew();
                SweepPrompt? first = null;
                while (!ct.IsCancellationRequested && first is null && !cm.IsEnded && idle.Elapsed < TimeSpan.FromSeconds(5))
                {
                    dialog.Prompts.TryTake(out first, 50);
                }

                if (first is not null)
                {
                    await Task.Delay(dwell, ct).ConfigureAwait(false);   // the client draws the page
                }

                cm.End();
                _conversation = null;
                if (cm.Error is not null)
                {
                    AppendSweepLine($"# script error {npcId}: {cm.Error.Message}");
                }

                if (first is not null && _player is not null)
                {
                    await MovePlayerToMapAsync(session, _player.Character.MapId, spawnPortal: 0).ConfigureAwait(false);
                    await Task.Delay(TimeSpan.FromMilliseconds(700), ct).ConfigureAwait(false);   // let the field reload
                }
            }

            _sweepCurrentNpc = 0;
            if (!ct.IsCancellationRequested)
            {
                AppendSweepLine("# npcs done");
                Console.WriteLine("[sweep] npcs done — every scripted NPC's dialog rendered without losing the client");
                await ReplyAsync(session, "sweep: 全 NPC 会話完了。落ちた NPC はありません").ConfigureAwait(false);
                _sweep = null;   // finished: a later logout is a logout, not a crash
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

    /// <summary>The quest script page on the client right now ("2300 start"), empty between quests.</summary>
    private string _sweepCurrentQuest = string.Empty;

    /// <summary>
    /// <c>/sweep quests</c>: the same rendering check for quest scripts. A quest script is not
    /// reachable by clicking — the client opens it from the journal — so neither /talk nor the NPC
    /// sweep ever draws one. This walks both sides (start() and end()) of every scripted quest,
    /// drawing each first page under the NPC the JMS Check data names for that side, so a dialog
    /// tag or an item/map token the client cannot render shows up here rather than in a player's
    /// session. Read-only: the stand-in character drops every grant, warp and quest record.
    /// </summary>
    private async Task RunQuestSweepAsync(MapleSession session, List<int> questIds, TimeSpan dwell, CancellationToken ct)
    {
        try
        {
            AppendSweepLine($"# sweep quests {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {questIds.Count} scripted quests, {dwell.TotalSeconds:0.##}s per page");
            int shown = 0;
            for (int i = 0; i < questIds.Count && !ct.IsCancellationRequested && _player is not null; i++)
            {
                int questId = questIds[i];
                QuestData? quest = _quests.GetQuest(questId);
                foreach (bool ending in new[] { false, true })
                {
                    if (ct.IsCancellationRequested || _player is null)
                    {
                        break;
                    }

                    string side = ending ? "end" : "start";
                    int npcId = (ending ? quest?.EndCheck?.Npc : quest?.StartCheck?.Npc) ?? 0;
                    if (npcId == 0 || (_npcNames is not null && !_npcNames.HasImage(npcId)))
                    {
                        // The page needs a portrait the client has; without one the dialog kills it.
                        // A quest side with no script at all is skipped silently by StartQuest below.
                        continue;
                    }

                    _conversation?.End();
                    var dialog = new SweepNpcDialog(new ChannelNpcDialog(session, _packets));
                    NpcConversation? qm = _npcScripts?.StartQuest(questId, npcId, dialog, new SweepScriptPlayer(_player.Character), ending);
                    if (qm is null)
                    {
                        continue;   // this quest scripts only the other side
                    }

                    shown++;
                    _sweepCurrentQuest = $"{questId} {side}";
                    AppendSweepLine($"quest\t{questId}\t{side}\t{npcId}\t{DateTime.Now:HH:mm:ss}");
                    Console.WriteLine($"[sweep] quest {i + 1}/{questIds.Count} {questId} {side} (npc {npcId})");
                    await ReplyAsync(session, $"[sweep quest {i + 1}/{questIds.Count}] {questId} {side}").ConfigureAwait(false);

                    // First page only, for the reason RunNpcSweepAsync documents: a second script
                    // message over an unanswered dialog makes the client close its socket.
                    _conversation = qm;
                    var idle = System.Diagnostics.Stopwatch.StartNew();
                    SweepPrompt? first = null;
                    while (!ct.IsCancellationRequested && first is null && !qm.IsEnded && idle.Elapsed < TimeSpan.FromSeconds(5))
                    {
                        dialog.Prompts.TryTake(out first, 50);
                    }

                    if (first is not null)
                    {
                        await Task.Delay(dwell, ct).ConfigureAwait(false);   // the client draws the page
                    }

                    qm.End();
                    _conversation = null;
                    if (qm.Error is not null)
                    {
                        AppendSweepLine($"# script error quest {questId} {side}: {qm.Error.Message}");
                    }

                    if (first is not null && _player is not null)
                    {
                        await MovePlayerToMapAsync(session, _player.Character.MapId, spawnPortal: 0).ConfigureAwait(false);
                        await Task.Delay(TimeSpan.FromMilliseconds(700), ct).ConfigureAwait(false);   // let the field reload
                    }
                }
            }

            _sweepCurrentQuest = string.Empty;
            if (!ct.IsCancellationRequested)
            {
                AppendSweepLine("# quests done");
                Console.WriteLine($"[sweep] quests done — {shown} quest pages rendered without losing the client");
                await ReplyAsync(session, $"sweep: 全クエスト会話完了（{shown} ページ）。落ちたクエストはありません").ConfigureAwait(false);
                _sweep = null;   // finished: a later logout is a logout, not a crash
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[sweep] quests stopped: {ex.Message}");
        }
    }
}
