// ChannelHandler partial: quest requests, gates, acts, kill counters.
using System.Security.Cryptography;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;

namespace Cronus.Server.Channel;

public sealed partial class ChannelHandler
{
    /// <summary>
    /// Handles <c>CP_UserQuestRequest</c> — accepting / completing / forfeiting a quest through the
    /// client's quest dialog (ports <c>ReqCUser.OnUserQuestRequest</c> + <c>MapleQuest</c>).
    /// Accept gates on the start check's level, seeds the mob-kill progress, and applies the start
    /// acts; complete verifies the end check (kills + items), applies the rewards (exp / meso /
    /// fame / items, negative counts taken away), and plays the completion effect. Script-driven
    /// quests run <c>scripts/quest/{questId}.js</c> (<c>start()</c> / <c>end()</c> with the global
    /// <c>qm</c>); lost-item recovery isn't modelled yet.
    /// </summary>
    private async ValueTask HandleQuestRequestAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null)
        {
            return;
        }

        byte action = packet.ReadByte();
        int questId = packet.ReadShort() & 0xFFFF;
        Character c = _player.Character;

        switch (action)
        {
            case QuestReqLostItem:
            {
                // [time:4][itemId:4] — re-grant a lost quest item the start act originally gave
                // (ports MapleQuestAction.RestoreLostItem: only if the player no longer has one).
                packet.ReadInt();
                int itemId = packet.ReadInt();
                QuestData? quest = _quests.GetQuest(questId);
                if (quest?.StartAct is { } act
                    && act.Items.Any(i => i.ItemId == itemId)
                    && CountInventoryItem(c, itemId) < 1)
                {
                    await ScriptGainItemAsync(session, itemId, 1).ConfigureAwait(false);
                }

                break;
            }

            case QuestReqAccept:
            {
                int npcId = packet.ReadInt();
                await AcceptQuestAsync(session, c, questId, npcId).ConfigureAwait(false);
                break;
            }

            case QuestReqOpeningScript: // scripts/quest/{questId}.js start(); plain accept if none
            {
                int npcId = packet.ReadInt();
                if (!TryStartQuestScript(session, questId, npcId, ending: false))
                {
                    await AcceptQuestAsync(session, c, questId, npcId).ConfigureAwait(false);
                }

                break;
            }

            case QuestReqComplete:
            {
                int npcId = packet.ReadInt();
                int selection = packet.Remaining >= 4 ? packet.ReadInt() : -1;
                await CompleteQuestAsync(session, c, questId, npcId, selection).ConfigureAwait(false);
                break;
            }

            case QuestReqCompleteScript: // scripts/quest/{questId}.js end(); plain complete if none
            {
                int npcId = packet.ReadInt();
                if (!TryStartQuestScript(session, questId, npcId, ending: true))
                {
                    await CompleteQuestAsync(session, c, questId, npcId).ConfigureAwait(false);
                }

                break;
            }

            case QuestReqResign:
                if (c.StartedQuests.Remove(questId))
                {
                    _characters.Save(c);
                    await session.SendAsync(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordNone)).ConfigureAwait(false);
                }

                break;
        }
    }

    /// <summary>
    /// Runs a quest's script (ports <c>TacosScriptQuest.startQuest/endQuest</c>): the script drives
    /// the dialog through <c>qm</c> and grants/verifies through <c>player</c>. False when the quest
    /// has no script (caller falls back to the data-driven path) or a conversation is already open.
    /// </summary>
    private bool TryStartQuestScript(MapleSession session, int questId, int npcId, bool ending)
    {
        if (_npcScripts is null || _conversation is { IsEnded: false })
        {
            return false;
        }

        var dialog = new ChannelNpcDialog(session, _packets);
        NpcConversation? conversation = _npcScripts.StartQuest(questId, npcId, dialog, CreateScriptPlayer(session), ending);
        if (conversation is null)
        {
            return false;
        }

        _conversation = conversation;
        return true;
    }

    private async ValueTask AcceptQuestAsync(MapleSession session, Character c, int questId, int npcId)
    {
        if (c.StartedQuests.ContainsKey(questId))
        {
            return;
        }

        QuestData? quest = _quests.GetQuest(questId);
        if (c.CompletedQuests.TryGetValue(questId, out long completedAt))
        {
            // Repeatable quests (wz "interval", minutes): re-acceptable once the interval has
            // passed since the last completion (ports MapleQuestRequirement.interval).
            int interval = quest?.StartCheck?.IntervalMinutes ?? 0;
            long intervalTicks = interval * 60L * 10_000_000L; // FILETIME is 100ns ticks
            if (interval <= 0 || CharacterDataEncoder.FileTimeNow() - completedAt < intervalTicks)
            {
                return;
            }

            c.CompletedQuests.Remove(questId);
        }

        if (quest?.StartCheck is { } start)
        {
            if (start.LevelMin > 0 && c.Level < start.LevelMin)
            {
                return; // under-leveled
            }

            if (start.LevelMax > 0 && c.Level > start.LevelMax)
            {
                return; // over-leveled (lvmax)
            }

            foreach (QuestItemEntry required in start.Items)
            {
                if (required.Count > 0 && CountInventoryItem(c, required.ItemId) < required.Count)
                {
                    return; // start-side item requirement not held
                }
            }

            foreach (QuestPrereq prereq in start.Quests)
            {
                bool met = prereq.State == 1
                    ? c.StartedQuests.ContainsKey(prereq.QuestId)
                    : c.CompletedQuests.ContainsKey(prereq.QuestId);
                if (!met)
                {
                    return; // prerequisite quest not at the required state
                }
            }

            if (start.Jobs.Count > 0 && !start.Jobs.Contains(c.Job))
            {
                return; // wrong job
            }
        }

        string progress = InitialQuestProgress(quest);
        c.StartedQuests[questId] = progress;

        if (quest?.StartAct is { } act)
        {
            await ApplyQuestActAsync(session, c, act).ConfigureAwait(false);
        }

        _characters.Save(c);
        await session.SendAsync(_packets.UserQuestResult(questId, npcId)).ConfigureAwait(false);
        await session.SendAsync(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordStarted, progress)).ConfigureAwait(false);
    }

    private async ValueTask CompleteQuestAsync(MapleSession session, Character c, int questId, int npcId, int selection = -1)
    {
        if (!c.StartedQuests.ContainsKey(questId))
        {
            return;
        }

        QuestData? quest = _quests.GetQuest(questId);
        if (quest is null || !QuestRequirementsMet(c, quest))
        {
            return; // unknown quest or unmet kills/items — the dialog stays open
        }

        if (quest.EndAct is { } act)
        {
            await ApplyQuestActAsync(session, c, act, selection).ConfigureAwait(false);
        }

        c.StartedQuests.Remove(questId);
        c.CompletedQuests[questId] = CharacterDataEncoder.FileTimeNow();
        _characters.Save(c);

        // nextQuest chains the client straight into the follow-up quest's dialog — the
        // tutorial/beginner lines (1000 -> 1001 -> ...) flow through this.
        short nextQuest = (short)(quest.EndAct?.NextQuest ?? 0);
        await session.SendAsync(_packets.UserQuestResult(questId, npcId, nextQuest)).ConfigureAwait(false);
        await session.SendAsync(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordCompleted)).ConfigureAwait(false);
        await session.SendAsync(_packets.UserEffectLocal(ChannelPackets.UserEffectQuestComplete)).ConfigureAwait(false);
        if (_field is not null)
        {
            await _field.BroadcastAsync(
                _packets.UserEffectRemote(c.Id, ChannelPackets.UserEffectQuestComplete),
                exceptCharacterId: c.Id).ConfigureAwait(false);
        }
    }

    /// <summary>Zeroed per-mob progress ("000" per required mob) for a fresh quest record.</summary>
    private static string InitialQuestProgress(QuestData? quest)
        => quest?.EndCheck is { Mobs.Count: > 0 } end
            ? string.Concat(Enumerable.Repeat("000", end.Mobs.Count))
            : string.Empty;

    /// <summary>All end-check kills reached and required items held.</summary>
    private bool QuestRequirementsMet(Character c, QuestData quest)
    {
        if (quest.EndCheck is not { } check)
        {
            return true;
        }

        if (check.Mobs.Count > 0)
        {
            string progress = c.StartedQuests.TryGetValue(quest.QuestId, out string? p) ? p : string.Empty;
            for (int i = 0; i < check.Mobs.Count; i++)
            {
                if (QuestProgressCount(progress, i) < check.Mobs[i].Count)
                {
                    return false;
                }
            }
        }

        foreach (QuestItemEntry req in check.Items)
        {
            if (req.Count > 0 && CountInventoryItem(c, req.ItemId) < req.Count)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Applies a quest act: give/take items, meso, fame, and exp. Selectable rewards (prop == -1)
    /// give only the row the player picked (<paramref name="selection"/> indexes the selectable
    /// rows in wz order); weighted-lottery rows (prop &gt; 0) aren't modelled yet and are skipped.
    /// </summary>
    private async ValueTask ApplyQuestActAsync(MapleSession session, Character c, QuestAct act, int selection = -1)
    {
        var changes = new List<InventoryChange>();
        QuestItemEntry? lotteryPick = PickLotteryReward(act.Items);
        int selectableIndex = 0;
        foreach (QuestItemEntry item in act.Items)
        {
            if (item.Prop is -1)
            {
                if (selectableIndex++ != selection)
                {
                    continue; // not the row the player chose
                }
            }
            else if (item.Prop is > 0)
            {
                if (!ReferenceEquals(item, lotteryPick))
                {
                    continue; // lottery: only the weighted-random winner is given
                }
            }
            else if (item.Prop is not null)
            {
                continue; // prop 0: unused marker rows
            }

            if (item.Count > 0)
            {
                int slotMax = _items.GetConsume(item.ItemId)?.SlotMax ?? Inventory.DefaultSlotMax;
                changes.AddRange(Inventory.Add(c, item.ItemId, item.Count, slotMax));
            }
            else if (item.Count < 0)
            {
                changes.AddRange(RemoveInventoryQuantity(c, item.ItemId, -item.Count));
            }
        }

        PopulateEquipStats(changes);
        if (changes.Count > 0)
        {
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }

        StatFlag flags = 0;
        if (act.Money != 0)
        {
            c.Meso = (int)Math.Clamp((long)c.Meso + act.Money, 0, int.MaxValue);
            flags |= StatFlag.Meso;
        }

        if (act.Fame != 0)
        {
            c.Fame = (short)Math.Clamp(c.Fame + act.Fame, -30000, 30000);
            flags |= StatFlag.Fame;
        }

        // SP grants (ports MapleQuestAction "sp"): a job filter row applies once the player's job
        // has reached one of the listed jobs (the reference then picks that job's skill book —
        // pre-BB non-Evan characters have a single book, which is what Character.Sp models).
        foreach (QuestSpEntry sp in act.SpGrants)
        {
            if (sp.Jobs.Count == 0 || sp.Jobs.Any(j => c.Job >= j))
            {
                c.Sp = (short)Math.Clamp(c.Sp + sp.SpValue, 0, short.MaxValue);
                flags |= StatFlag.Sp;
            }
        }

        if (flags != 0)
        {
            await session.SendAsync(_packets.StatChanged(c, flags)).ConfigureAwait(false);
        }

        if (act.Money > 0)
        {
            await session.SendAsync(_packets.IncMoneyMessage(act.Money)).ConfigureAwait(false);
        }

        if (act.Exp > 0 && _player is not null)
        {
            await GrantExpToAsync(_player, act.Exp).ConfigureAwait(false);
        }

        // Skill grants (ports MapleQuestAction "skill"): honour the wz job filter, set the learned
        // level, and push the record so the client updates its skill window immediately.
        foreach (QuestSkillEntry skill in act.Skills)
        {
            if (skill.Jobs.Count > 0 && !skill.Jobs.Contains(c.Job))
            {
                continue;
            }

            c.Skills[skill.SkillId] = skill.SkillLevel;
            await session.SendAsync(
                _packets.ChangeSkillRecordResult(skill.SkillId, skill.SkillLevel, skill.MasterLevel)).ConfigureAwait(false);
        }

        // Buff-item act (ports MapleQuestAction "buffItemID": apply the item's effect directly).
        if (act.BuffItemId > 0 && _items.GetConsume(act.BuffItemId) is { } buffSpec)
        {
            await ApplyItemBuffAsync(session, buffSpec).ConfigureAwait(false);
        }

        // Other quests' state changes (ports MapleQuestAction "quest"): 1 = mark started,
        // 2 = mark completed, anything else clears the record.
        foreach (QuestPrereq state in act.QuestStates)
        {
            switch (state.State)
            {
                case 1:
                    c.StartedQuests[state.QuestId] = string.Empty;
                    await session.SendAsync(_packets.QuestRecordMessage(state.QuestId, ChannelPackets.QuestRecordStarted)).ConfigureAwait(false);
                    break;
                case 2:
                    c.StartedQuests.Remove(state.QuestId);
                    c.CompletedQuests[state.QuestId] = CharacterDataEncoder.FileTimeNow();
                    await session.SendAsync(_packets.QuestRecordMessage(state.QuestId, ChannelPackets.QuestRecordCompleted)).ConfigureAwait(false);
                    break;
                default:
                    c.StartedQuests.Remove(state.QuestId);
                    await session.SendAsync(_packets.QuestRecordMessage(state.QuestId, ChannelPackets.QuestRecordNone)).ConfigureAwait(false);
                    break;
            }
        }
    }

    /// <summary>Picks the weighted-random winner among lottery (<c>prop &gt; 0</c>) reward rows.</summary>
    private static QuestItemEntry? PickLotteryReward(IReadOnlyList<QuestItemEntry> items)
    {
        int total = 0;
        foreach (QuestItemEntry item in items)
        {
            if (item.Prop is > 0)
            {
                total += item.Prop.Value;
            }
        }

        if (total <= 0)
        {
            return null;
        }

        int roll = Random.Shared.Next(total);
        foreach (QuestItemEntry item in items)
        {
            if (item.Prop is > 0)
            {
                roll -= item.Prop.Value;
                if (roll < 0)
                {
                    return item;
                }
            }
        }

        return null;
    }

    /// <summary>Removes a total quantity of an item across its inventory slots.</summary>
    private static List<InventoryChange> RemoveInventoryQuantity(Character c, int itemId, int quantity)
    {
        var changes = new List<InventoryChange>();
        int tab = Inventory.Tab(itemId);
        int remaining = quantity;
        foreach (InventoryItem item in c.EquippedItems
                     .Where(i => i.ItemId == itemId && i.Position > 0)
                     .OrderBy(i => i.Position)
                     .ToList())
        {
            if (remaining <= 0)
            {
                break;
            }

            int take = Math.Min(remaining, item.Quantity);
            if (Inventory.RemoveFromSlot(c, tab, item.Position, take) is { } change)
            {
                changes.Add(change);
            }

            remaining -= take;
        }

        return changes;
    }

    private static int CountInventoryItem(Character c, int itemId)
        => c.EquippedItems.Where(i => i.ItemId == itemId && i.Position > 0).Sum(i => i.Quantity);

    /// <summary>The 3-digit kill count at a mob index of a quest progress string.</summary>
    private static int QuestProgressCount(string progress, int index)
    {
        int start = index * 3;
        return start + 3 <= progress.Length && int.TryParse(progress.AsSpan(start, 3), out int n) ? n : 0;
    }

    /// <summary>Rebuilds a progress string with one mob's count changed (3 digits per mob).</summary>
    private static string SetQuestProgressCount(string progress, int mobCount, int index, int value)
    {
        char[] buffer = new char[mobCount * 3];
        for (int i = 0; i < mobCount; i++)
        {
            int v = i == index ? value : QuestProgressCount(progress, i);
            Math.Clamp(v, 0, 999).ToString("000").CopyTo(0, buffer, i * 3, 3);
        }

        return new string(buffer);
    }

    /// <summary>
    /// Advances the killer's in-progress kill quests for a slain mob and pushes the journal update
    /// (ports <c>MapleQuestStatus.mobKilled</c> + <c>ResWrapper.updateQuestMobKills</c>: per-mob
    /// 3-digit counts in the quest's Check order).
    /// </summary>
    private async ValueTask UpdateQuestKillsAsync(MapleSession session, int mobTemplateId)
    {
        if (_player is null || _player.Character.StartedQuests.Count == 0)
        {
            return;
        }

        Character c = _player.Character;
        List<(int QuestId, string Progress)>? updates = null;
        foreach (KeyValuePair<int, string> entry in c.StartedQuests.ToList())
        {
            if (_quests.GetQuest(entry.Key)?.EndCheck is not { Mobs.Count: > 0 } check)
            {
                continue;
            }

            int index = -1;
            for (int i = 0; i < check.Mobs.Count; i++)
            {
                if (check.Mobs[i].MobId == mobTemplateId)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                continue;
            }

            int current = QuestProgressCount(entry.Value, index);
            if (current >= check.Mobs[index].Count)
            {
                continue; // this mob's requirement is already met
            }

            string updated = SetQuestProgressCount(entry.Value, check.Mobs.Count, index, current + 1);
            c.StartedQuests[entry.Key] = updated;
            (updates ??= new List<(int, string)>()).Add((entry.Key, updated));
        }

        if (updates is null)
        {
            return;
        }

        _characters.Save(c);
        foreach ((int questId, string progress) in updates)
        {
            await session.SendAsync(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordStarted, progress)).ConfigureAwait(false);
        }
    }
}
