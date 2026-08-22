// ChannelHandler partial: GM commands, NPC selection, scripts, map movement.
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
    /// Minimal GM/debug command set for local testing (chat lines starting with '/'). Replies
    /// are echoed back to the caller as their own chat line. Documented in docs/COMMANDS.md
    /// (Japanese: docs/COMMANDS.ja.md) — keep those in sync when commands change.
    /// </summary>
    private async ValueTask HandleCommandAsync(MapleSession session, string command)
    {
        string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        switch (parts[0].ToLowerInvariant())
        {
            case "map" when parts.Length >= 2 && int.TryParse(parts[1], out int mapId):
                await MovePlayerToMapAsync(session, mapId, spawnPortal: 0).ConfigureAwait(false);
                break;

            case "meso" when parts.Length >= 2 && int.TryParse(parts[1], out int amount):
                _player!.Character.Meso = (int)Math.Clamp((long)_player.Character.Meso + amount, 0, int.MaxValue);
                _characters.Save(_player.Character);
                await session.SendAsync(_packets.StatChanged(_player.Character, StatFlag.Meso)).ConfigureAwait(false);
                break;

            case "notice" when parts.Length >= 2:
                await _field!.BroadcastAsync(_packets.BroadcastNotice(command["notice ".Length..].Trim()))
                    .ConfigureAwait(false);
                break;

            case "snotice" when parts.Length >= 2:
            {
                byte[] notice = _packets.BroadcastNotice(command["snotice ".Length..].Trim());
                foreach (Field f in _fields.Fields)
                {
                    await f.BroadcastAsync(notice).ConfigureAwait(false);
                }

                break;
            }

            case "heal":
            {
                Character hc = _player!.Character;
                hc.Hp = hc.MaxHp;
                hc.Mp = hc.MaxMp;
                await session.SendAsync(_packets.StatChanged(hc, StatFlag.Hp | StatFlag.Mp)).ConfigureAwait(false);
                await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false); // party sees the heal
                break;
            }

            case "job" when parts.Length >= 2 && int.TryParse(parts[1], out int job):
                await SetStatAsync(session, StatFlag.Job, c => c.Job = (short)job).ConfigureAwait(false);
                break;

            case "level" when parts.Length >= 2 && int.TryParse(parts[1], out int level):
            {
                Character lc = _player!.Character;
                int target = Math.Clamp(level, 1, 200);
                StatFlag levelChanged = StatFlag.Level | StatFlag.Exp;
                if (target > lc.Level)
                {
                    // Raising runs real level-ups so HP/MP/AP/SP grow like normal play.
                    levelChanged |= CharacterProgression.ForceLevelUps(lc, target - lc.Level, EffectResolverFor(lc));
                }
                else
                {
                    lc.Level = (byte)target; // lowering just sets the level (stats keep their values)
                }

                lc.Exp = 0; // reset so the new level's bar starts clean
                _characters.Save(lc);
                await session.SendAsync(_packets.StatChanged(lc, levelChanged)).ConfigureAwait(false);
                await RefreshPartyWindowAsync(_player).ConfigureAwait(false); // party window shows levels
                if (lc.GuildId > 0)
                {
                    await BroadcastToGuildAsync(lc.GuildId, _packets.GuildMemberLevelJob(lc.GuildId, lc.Id, lc.Level, lc.Job), exceptCharacterId: lc.Id).ConfigureAwait(false);
                }

                break;
            }

            case "hp" when parts.Length >= 2 && int.TryParse(parts[1], out int hp):
            {
                Character sc = _player!.Character;
                sc.Hp = (short)Math.Clamp(hp, 0, sc.MaxHp);
                _characters.Save(sc);
                await session.SendAsync(_packets.StatChanged(sc, StatFlag.Hp)).ConfigureAwait(false);
                await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false);
                break;
            }

            case "maxhp" when parts.Length >= 2 && int.TryParse(parts[1], out int maxHp):
            {
                Character sc = _player!.Character;
                sc.MaxHp = (short)Math.Clamp(maxHp, 1, 30000);
                sc.Hp = Math.Min(sc.Hp, sc.MaxHp);
                _characters.Save(sc);
                await session.SendAsync(_packets.StatChanged(sc, StatFlag.Hp | StatFlag.MaxHp)).ConfigureAwait(false);
                await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false);
                break;
            }

            case "mp" when parts.Length >= 2 && int.TryParse(parts[1], out int mp):
                await SetStatAsync(session, StatFlag.Mp, c => c.Mp = (short)Math.Clamp(mp, 0, c.MaxMp)).ConfigureAwait(false);
                break;

            case "maxmp" when parts.Length >= 2 && int.TryParse(parts[1], out int maxMp):
                await SetStatAsync(session, StatFlag.Mp | StatFlag.MaxMp, c =>
                {
                    c.MaxMp = (short)Math.Clamp(maxMp, 1, 30000);
                    c.Mp = Math.Min(c.Mp, c.MaxMp);
                }).ConfigureAwait(false);
                break;

            case "maxskills":
            {
                Character sc = _player!.Character;
                int learned = 0;
                foreach (int jobFile in JobSkillBooks(sc.Job))
                {
                    foreach (int skillId in _skills.GetSkillIds(jobFile))
                    {
                        int max = _skills.GetMaxLevel(skillId);
                        if (max > 0)
                        {
                            sc.Skills[skillId] = max;
                            await session.SendAsync(_packets.ChangeSkillRecordResult(skillId, max)).ConfigureAwait(false);
                            learned++;
                        }
                    }
                }

                _characters.Save(sc);
                await ReplyAsync(session, $"maxed {learned} skills for job {sc.Job}").ConfigureAwait(false);
                break;
            }

            case "gender":
            {
                // Toggle (or set: /gender m|f) the character's gender, then bounce the client
                // through a same-channel migration so it re-enters with the new look — gender
                // rides in the entry CharacterData/AvatarLook, which only a re-entry redraws.
                Character gc = _player!.Character;
                byte newGender = parts.Length >= 2
                    ? parts[1].ToLowerInvariant() switch
                    {
                        "m" or "male" or "0" or "男" => (byte)0,
                        "f" or "female" or "1" or "女" => (byte)1,
                        _ => gc.Gender,
                    }
                    : (byte)(gc.Gender == 0 ? 1 : 0);
                gc.Gender = newGender;
                _characters.Save(gc);

                // The cash shop filters its catalog by the ACCOUNT gender delivered at login, so
                // flip the account too (GameConstants gate) — it takes effect on the next login.
                if (GameConstants.GenderCommandChangesAccount
                    && _accounts?.FindById(gc.AccountId) is { } genderAccount)
                {
                    genderAccount.Gender = newGender;
                    _accounts.Save(genderAccount);
                }

                await ReplyAsync(session, newGender == 0 ? "gender → 男 (male)" : "gender → 女 (female)").ConfigureAwait(false);
                await ReplyAsync(session, "ポイントショップの性別反映には再ログインしてください").ConfigureAwait(false);

                if (_channelEndpoints is { } eps && _channelId >= 0 && _channelId < eps.Count)
                {
                    System.Net.IPEndPoint self = eps[_channelId];
                    await session.SendAsync(_packets.MigrateCommand(self.Address, self.Port)).ConfigureAwait(false);
                }
                else
                {
                    await ReplyAsync(session, "再ログインで見た目に反映されます").ConfigureAwait(false);
                }

                break;
            }

            case "beauty":
            {
                // Opens the style console: a windowed picker over EVERY hair style / hair color /
                // face / eye color / skin from the wz data — no ids to type. Driven as a C#-side
                // conversation over the same dialog plumbing the NPC scripts use.
                if (_conversation is { IsEnded: false })
                {
                    break; // another dialog is open
                }

                if (_styles is null)
                {
                    await ReplyAsync(session, "スタイルデータが未ロードです (CRONUS_WZ)").ConfigureAwait(false);
                    break;
                }

                var beautyDialog = new ChannelNpcDialog(session, _packets);
                var beautyConvo = new NpcConversation(BeautyNpcId, beautyDialog);
                _conversation = beautyConvo;
                ChannelPlayer beautyPlayer = CreateScriptPlayer(session);
                IStyleProvider beautyStyles = _styles;
                var beautyThread = new Thread(() => RunBeautyFlow(beautyConvo, beautyPlayer, beautyStyles))
                {
                    IsBackground = true,
                    Name = "beauty-console",
                };
                beautyThread.Start();
                break;
            }

            case "booktest" when parts.Length >= 2:
            {
                // Live bisect for the card-pickup crash: choose WHICH monster-book packets the
                // next pickup sends. No restart needed. "reset" clears this character's
                // registered cards so the same mob drops its card again.
                string mode = parts[1].ToLowerInvariant();
                if (mode == "reset")
                {
                    _player!.Character.MonsterCards.Clear();
                    _characters.Save(_player.Character);
                    await ReplyAsync(session, "monster book cleared — cards will drop again").ConfigureAwait(false);
                    break;
                }

                GameConstants.SendMonsterBookSetCard = mode is "set" or "all";
                GameConstants.SendMonsterBookCardEffect = mode is "effect" or "all";
                GameConstants.SendMonsterBookCardMessage = mode is "msg" or "all";
                if (mode == "effect" && parts.Length >= 3 && byte.TryParse(parts[2], out byte effectValue))
                {
                    GameConstants.MonsterBookCardEffectValue = effectValue; // e.g. /booktest effect 16
                }

                await ReplyAsync(session,
                    $"book packets: set={GameConstants.SendMonsterBookSetCard} effect={GameConstants.SendMonsterBookCardEffect}(value {GameConstants.MonsterBookCardEffectValue}) msg={GameConstants.SendMonsterBookCardMessage}").ConfigureAwait(false);
                break;
            }

            case "clearinv":
            {
                // Empties inventory tabs (positive slots only — worn equips stay): /clearinv wipes
                // all five tabs, /clearinv <1-5> just one. Sends the per-slot removes so the
                // client's grid clears live.
                Character cc = _player!.Character;
                int? onlyTab = parts.Length >= 2 && int.TryParse(parts[1], out int t) && t is >= 1 and <= 5 ? t : null;
                var removed = new List<InventoryChange>();
                foreach (InventoryItem item in cc.EquippedItems
                             .Where(i => i.Position > 0 && (onlyTab is null || Inventory.Tab(i.ItemId) == onlyTab))
                             .ToList())
                {
                    cc.EquippedItems.Remove(item);
                    removed.Add(new InventoryChange(InvMode.Remove, Inventory.Tab(item.ItemId), item.Position, null, 0));
                }

                _characters.Save(cc);
                foreach (InventoryChange[] chunk in removed.Chunk(32)) // keep packets small
                {
                    await session.SendAsync(_packets.InventoryOperation(chunk)).ConfigureAwait(false);
                }

                await ReplyAsync(session, $"cleared {removed.Count} item(s)").ConfigureAwait(false);
                break;
            }

            case "questreset" when parts.Length >= 2 && int.TryParse(parts[1], out int resetQuestId):
            {
                // Clears one quest from both records (debug/bot use: makes quest flows re-runnable).
                Character qc = _player!.Character;
                bool removed = qc.StartedQuests.Remove(resetQuestId) | qc.CompletedQuests.Remove(resetQuestId);
                _characters.Save(qc);
                if (removed)
                {
                    await session.SendAsync(_packets.QuestRecordMessage(resetQuestId, ChannelPackets.QuestRecordNone)).ConfigureAwait(false);
                }

                await ReplyAsync(session, $"quest {resetQuestId} reset").ConfigureAwait(false);
                break;
            }

            case "guildcreate" when parts.Length >= 2:
                // Free, works anywhere (the client's own flow needs the HQ map and 5m meso).
                await CreateGuildAsync(session, _player!.Character, parts[1], cost: 0).ConfigureAwait(false);
                break;

            case "str" when parts.Length >= 2 && int.TryParse(parts[1], out int str):
                await SetStatAsync(session, StatFlag.Str, c => c.Str = (short)Math.Clamp(str, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "dex" when parts.Length >= 2 && int.TryParse(parts[1], out int dex):
                await SetStatAsync(session, StatFlag.Dex, c => c.Dex = (short)Math.Clamp(dex, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "int" when parts.Length >= 2 && int.TryParse(parts[1], out int intStat):
                await SetStatAsync(session, StatFlag.Int, c => c.Int = (short)Math.Clamp(intStat, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "luk" when parts.Length >= 2 && int.TryParse(parts[1], out int luk):
                await SetStatAsync(session, StatFlag.Luk, c => c.Luk = (short)Math.Clamp(luk, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "ap" when parts.Length >= 2 && int.TryParse(parts[1], out int ap):
                await SetStatAsync(session, StatFlag.Ap, c => c.Ap = (short)Math.Clamp(c.Ap + ap, 0, short.MaxValue)).ConfigureAwait(false);
                break;

            case "sp" when parts.Length >= 2 && int.TryParse(parts[1], out int sp):
                await SetStatAsync(session, StatFlag.Sp, c => c.Sp = (short)Math.Clamp(c.Sp + sp, 0, short.MaxValue)).ConfigureAwait(false);
                break;

            case "fame" when parts.Length >= 2 && int.TryParse(parts[1], out int fame):
                await SetStatAsync(session, StatFlag.Fame, c => c.Fame = (short)Math.Clamp(fame, -30000, 30000)).ConfigureAwait(false);
                break;

            case "save":
                _characters.Save(_player!.Character);
                await ReplyAsync(session, "saved").ConfigureAwait(false);
                break;

            case "item" when parts.Length >= 2 && int.TryParse(parts[1], out int itemId):
            {
                int qty = parts.Length >= 3 && int.TryParse(parts[2], out int q) ? q : 1;
                Character ic = _player!.Character;
                int slotMax = _items.GetConsume(itemId)?.SlotMax ?? Inventory.DefaultSlotMax;
                List<InventoryChange> changes = Inventory.Add(ic, itemId, qty, slotMax);
                PopulateEquipStats(changes); // a spawned equip gets its wz base stats
                _characters.Save(ic);
                if (changes.Count > 0)
                {
                    await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
                }

                await ReplyAsync(session, $"added {itemId} x{qty}").ConfigureAwait(false);
                break;
            }

            case "drop" when parts.Length >= 2 && int.TryParse(parts[1], out int dropItemId) && _field is not null:
            {
                // Spawns a real ground drop at the player's feet (item id, or 0 for a meso pile),
                // so the full drop → pickup path can be exercised. Handy for client/bot testing.
                int amount = parts.Length >= 3 && int.TryParse(parts[2], out int a) ? a : 1;
                FieldPlayer dp = _player!;
                if (dropItemId == 0)
                {
                    FieldDrop meso = _field.AddPlayerMesoDrop(Math.Max(1, amount), dp.X, dp.Y, dp.Character.Id);
                    await _field.BroadcastAsync(_packets.DropEnterFieldMeso(meso)).ConfigureAwait(false);
                }
                else
                {
                    FieldDrop item = _field.AddItemDrop(dropItemId, (short)Math.Clamp(amount, 1, short.MaxValue), dp.X, dp.Y, source: null);
                    await _field.BroadcastAsync(_packets.DropEnterFieldItem(item)).ConfigureAwait(false);
                }

                await ReplyAsync(session, $"dropped {dropItemId} x{amount}").ConfigureAwait(false);
                break;
            }

            case "shop" when parts.Length >= 2 && int.TryParse(parts[1], out int shopId):
            {
                Shop? shop = _shops.GetShop(shopId);
                if (shop is null)
                {
                    await ReplyAsync(session, $"no shop {shopId}").ConfigureAwait(false);
                    break;
                }

                await OpenShopAsync(session, shop).ConfigureAwait(false);
                break;
            }

            case "storage":
                await OpenStorageAsync(session).ConfigureAwait(false);
                break;

            case "warp" when parts.Length >= 2:
            {
                FieldPlayer? target = _fields.FindPlayerByName(parts[1]);
                if (target is null || target.Character.Id == _player!.Character.Id)
                {
                    await ReplyAsync(session, $"'{parts[1]}' is not online").ConfigureAwait(false);
                    break;
                }

                await MovePlayerToMapAsync(session, target.Character.MapId, spawnPortal: 0).ConfigureAwait(false);
                break;
            }

            case "players":
            case "online":
            {
                var names = new List<string>();
                foreach (Field f in _fields.Fields)
                {
                    foreach (FieldPlayer fp in f.Players)
                    {
                        names.Add(fp.Character.Name);
                    }
                }

                await ReplyAsync(session, "online: " + (names.Count == 0 ? "(none)" : string.Join(", ", names)))
                    .ConfigureAwait(false);
                break;
            }

            case "pos":
                await ReplyAsync(session, $"pos: ({_player!.X}, {_player.Y}) map {_player.Character.MapId}")
                    .ConfigureAwait(false);
                break;

            case "help":
                await ReplyAsync(session, "commands: /map <id>, /warp <name>, /meso <n>, /heal, /job <n>, /level <n>, "
                    + "/hp /maxhp /mp /maxmp /str /dex /int /luk <n>, /ap <n>, /sp <n>, /fame <n>, "
                    + "/item <id> [qty], /drop <id> [qty], /shop <id>, /storage, /guildcreate <name>, /maxskills, /questreset <id>, /gender [m|f], /beauty, /clearinv [tab], /save, /players, /notice <msg>, /snotice <msg>, /pos, /help")
                    .ConfigureAwait(false);
                break;

            default:
                await ReplyAsync(session, $"unknown command: {parts[0]}").ConfigureAwait(false);
                break;
        }
    }

    /// <summary>Applies a stat mutation to the caller, persists it, and pushes the changed stat.</summary>
    private async ValueTask SetStatAsync(MapleSession session, StatFlag flag, Action<Character> mutate)
    {
        Character c = _player!.Character;
        mutate(c);
        _characters.Save(c);
        await session.SendAsync(_packets.StatChanged(c, flag)).ConfigureAwait(false);
    }

    /// <summary>
    /// The skill-book file ids a job can learn from: the beginner book, the 1st-job book, then
    /// each advancement up to the current code (e.g. 112 → 000, 100, 110, 111, 112).
    /// </summary>
    private static IEnumerable<int> JobSkillBooks(int job)
    {
        // The family's beginner book: 0 (explorer), 1000 (Noblesse), 2000 (Legend).
        yield return job >= 2000 ? 2000 : job >= 1000 ? 1000 : 0;
        if (job <= 0 || job is 1000 or 2000)
        {
            yield break;
        }

        int first = job / 100 * 100;
        yield return first;
        if (job == first)
        {
            yield break;
        }

        for (int j = job / 10 * 10; j <= job; j++)
        {
            yield return j;
        }
    }

    /// <summary>Sends a chat line visible only to the calling player (as their own message).</summary>
    private ValueTask ReplyAsync(MapleSession session, string text)
        => session.SendAsync(_packets.UserChat(_player!.Character.Id, isGm: true, text, onlyBalloon: false));

    private async ValueTask HandleSelectNpcAsync(MapleSession session, PacketReader packet)
    {
        // One conversation at a time; ignore a new NPC while a script is still running.
        if (_player is null || _conversation is { IsEnded: false })
        {
            return;
        }

        // JMS v186 CP_UserSelectNpc: [npcObjectId:4][x:2][y:2]. The client sends the runtime
        // object id; resolve it to the template id (the script/shop key) via the field.
        int objectId = packet.ReadInt();
        int templateId = _field?.FindNpc(objectId)?.TemplateId ?? objectId;

        // A vendor NPC opens its shop directly on click (ports MapleNPC.sendShop's auto-shop).
        Shop? shop = _shops.GetShopByNpc(templateId);
        if (shop is not null)
        {
            await OpenShopAsync(session, shop).ConfigureAwait(false);
            return;
        }

        if (_npcScripts is null)
        {
            return;
        }

        var dialog = new ChannelNpcDialog(session, _packets);
        _conversation = _npcScripts.Start(templateId, dialog, CreateScriptPlayer(session));

        // No script and no shop: send NOTHING (ports OnUserSelectNpc — TacosScriptNPC.start just
        // returns false). The client then runs its own quest UI for the NPC; a server-sent
        // greeting dialog here would swallow the click and break every quest-NPC conversation.
    }

    /// <summary>The NPC whose portrait fronts the /beauty console (the Henesys stylist).</summary>
    private const int BeautyNpcId = 1012103;

    /// <summary>Styles shown per picker page (the ask-avatar window renders a small grid).</summary>
    private const int BeautyPageSize = 8;

    /// <summary>
    /// The /beauty conversation: category menu → (for long lists) a page menu → the windowed
    /// style picker (SM_ASKAVATAR), then the change is applied through the same script-player
    /// ops the salons use (validated + avatar broadcast). Runs on its own thread and blocks on
    /// the client's answers exactly like a Jint NPC script.
    /// </summary>
    private static void RunBeautyFlow(NpcConversation cm, ChannelPlayer player, IStyleProvider styles)
    {
        try
        {
            int category = cm.askMenu("スタイルコンソールへようこそ！何を変えますか？\r\n"
                + "#L0#髪型#l\r\n#L1#髪色#l\r\n#L2#整形（顔）#l\r\n#L3#目の色#l\r\n#L4#肌の色#l");

            switch (category)
            {
                case 0: // hair style, keeping the current color where that variant exists
                {
                    int color = player.getHair() % 10;
                    int lo = player.getGender() == 0 ? 30000 : 31000;
                    List<int> candidates = styles.AllHairs()
                        .Where(h => h >= lo && h < lo + 1000)
                        .GroupBy(h => h / 10 * 10)
                        .Select(g => g.Contains(g.Key + color) ? g.Key + color : g.First())
                        .Distinct()
                        .ToList();
                    PickPagedStyle(cm, candidates, "髪型", picked => player.setHair(picked));
                    break;
                }

                case 1: // hair color: the current style's valid color variants
                {
                    int baseHair = player.getHair() / 10 * 10;
                    List<int> candidates = Enumerable.Range(0, 10)
                        .Select(c2 => baseHair + c2)
                        .Where(styles.IsValidHair)
                        .ToList();
                    PickPagedStyle(cm, candidates, "髪色", picked => player.setHair(picked));
                    break;
                }

                case 2: // face, keeping the current eye color where that variant exists
                {
                    int eyeColor = player.getFace() / 100 % 10;
                    int lo = player.getGender() == 0 ? 20000 : 21000;
                    List<int> candidates = styles.AllFaces()
                        .Where(f => f >= lo && f < lo + 1000)
                        .GroupBy(f => f - (f / 100 % 10) * 100)
                        .Select(g => g.Contains(g.Key + eyeColor * 100) ? g.Key + eyeColor * 100 : g.First())
                        .Distinct()
                        .ToList();
                    PickPagedStyle(cm, candidates, "顔", picked => player.setFace(picked));
                    break;
                }

                case 3: // eye color: the current face's valid color variants
                {
                    int baseFace = player.getFace() - (player.getFace() / 100 % 10) * 100;
                    List<int> candidates = Enumerable.Range(0, 9)
                        .Select(c2 => baseFace + (c2 * 100))
                        .Where(styles.IsValidFace)
                        .ToList();
                    PickPagedStyle(cm, candidates, "目の色", picked => player.setFace(picked));
                    break;
                }

                case 4: // skin
                {
                    List<int> candidates = styles.AllSkins().ToList();
                    PickPagedStyle(cm, candidates, "肌の色", picked => player.setSkin(picked));
                    break;
                }
            }
        }
        catch (ConversationEndedException)
        {
            // The player escaped the dialog — normal end.
        }
        catch (Exception)
        {
            // Never let a picker bug take the session down; the dialog just closes.
        }
        finally
        {
            cm.End();
        }
    }

    /// <summary>Pages <paramref name="candidates"/> through the avatar picker and applies the pick.</summary>
    private static void PickPagedStyle(NpcConversation cm, List<int> candidates, string what, Action<int> apply)
    {
        if (candidates.Count == 0)
        {
            cm.sendOk("選べる" + what + "が見つかりませんでした。");
            return;
        }

        int page = 0;
        int pages = (candidates.Count + BeautyPageSize - 1) / BeautyPageSize;
        if (pages > 1)
        {
            var menu = new System.Text.StringBuilder(what + " （全" + candidates.Count + "種）ページを選んでください:");
            for (int i = 0; i < pages; i++)
            {
                int from = i * BeautyPageSize + 1;
                int to = Math.Min(candidates.Count, (i + 1) * BeautyPageSize);
                menu.Append("\r\n#L").Append(i).Append('#').Append(from).Append('-').Append(to).Append("番#l");
            }

            page = cm.askMenu(menu.ToString());
            if (page < 0 || page >= pages)
            {
                return;
            }
        }

        int[] shown = candidates.Skip(page * BeautyPageSize).Take(BeautyPageSize).ToArray();
        int pick = cm.askAvatar("お好きな" + what + "を選んでください。", shown);
        if (pick < 0 || pick >= shown.Length)
        {
            return;
        }

        apply(shown[pick]);
        cm.sendOk("はい、できあがり！お似合いですよ。");
    }

    /// <summary>The <c>player</c> object handed to NPC / quest / portal scripts.</summary>
    private ChannelPlayer CreateScriptPlayer(MapleSession session) => new(
        _player!.Character, _characters, session, _packets,
        warp: (map, portal) => MovePlayerToMapAsync(session, map, portal),
        openShop: shopId => _shops.GetShop(shopId) is { } s ? OpenShopAsync(session, s) : ValueTask.CompletedTask,
        openStorage: () => OpenStorageAsync(session),
        gainItem: (itemId, quantity) => ScriptGainItemAsync(session, itemId, quantity),
        itemCount: itemId => CountInventoryItem(_player!.Character, itemId),
        effectOf: EffectResolverFor(_player!.Character),
        styles: _styles,
        avatarModified: () => _field is { } f
            ? f.BroadcastAsync(_packets.UserAvatarModified(_player!.Character), exceptCharacterId: _player!.Character.Id)
            : ValueTask.CompletedTask,
        hasMerchant: () => _merchants.GetByOwner(_player!.Character.Id) is not null,
        retrieveMerchant: RetrieveMerchantAsync,
        spawnMob: (mobId, count) => ScriptSpawnMobAsync(mobId, count),
        mobCount: () => _field?.Mobs.Count(m => !m.IsDead) ?? 0);

    /// <summary>Spawns mobs at the scripting player's feet (boss altars, event NPCs).</summary>
    private async ValueTask ScriptSpawnMobAsync(int mobId, int count)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        MobData? stats = _fields.MobProvider?.GetMob(mobId);
        for (int i = 0; i < Math.Clamp(count, 1, 20); i++)
        {
            FieldMob mob = _field.SpawnMob(mobId, stats, _player.X, _player.Y, foothold: 0);
            await _field.BroadcastAsync(_packets.MobEnterField(mob)).ConfigureAwait(false);
            mob.ControllerId = _player.Character.Id;
            await TrySendAsync(_player, _packets.MobChangeController(mob)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Packs up the player's hired merchant from afar (the Fredrick service): visitors are shown
    /// out, unsold stock and banked meso return to the owner. False when they have none.
    /// </summary>
    private async ValueTask<bool> RetrieveMerchantAsync()
    {
        if (_player is null || _merchants.GetByOwner(_player.Character.Id) is not { } merchant)
        {
            return false;
        }

        await CloseHiredMerchantAsync(merchant).ConfigureAwait(false);
        return true;
    }

    /// <summary>
    /// Gives (positive) or takes (negative) items on behalf of a script, pushing the live
    /// inventory update (the script-side equivalent of a quest act's item list).
    /// </summary>
    private async ValueTask ScriptGainItemAsync(MapleSession session, int itemId, int quantity)
    {
        if (_player is null || quantity == 0)
        {
            return;
        }

        Character c = _player.Character;
        List<InventoryChange> changes;
        if (quantity > 0)
        {
            int slotMax = _items.GetConsume(itemId)?.SlotMax ?? Inventory.DefaultSlotMax;
            changes = Inventory.Add(c, itemId, quantity, slotMax);
            PopulateEquipStats(changes); // a granted equip gets its wz base stats
        }
        else
        {
            changes = RemoveInventoryQuantity(c, itemId, -quantity);
        }

        if (changes.Count > 0)
        {
            _characters.Save(c);
            await session.SendAsync(_packets.InventoryOperation(changes)).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles <c>CP_UserPortalScriptRequest</c> — stepping on a scripted portal (ports
    /// <c>ReqCUser.OnUserPortalScriptRequest</c>). Looks up the portal on the current map and runs
    /// its script (which typically warps the player). Runs off the packet loop so a warp inside is
    /// safe. No-op if the portal has no script or scripting isn't configured.
    /// </summary>
    private async ValueTask HandlePortalScriptAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null || _portalScripts is null)
        {
            return;
        }

        // JMS v186 CP_UserPortalScriptRequest: [portalCount:1][portalName:str][x:2][y:2]
        packet.ReadByte();
        string portalName = packet.ReadString();

        PortalData? portal = _maps.GetMap(_player.Character.MapId)?.FindPortal(portalName);
        if (portal is null || !portal.HasScript)
        {
            return;
        }

        ChannelPlayer scriptPlayer = CreateScriptPlayer(session);
        await Task.Run(() => _portalScripts.Run(portal.Script, scriptPlayer)).ConfigureAwait(false);
    }

    private void HandleScriptAnswer(PacketReader packet)
    {
        NpcConversation? conversation = _conversation;
        if (conversation is null || conversation.IsEnded)
        {
            _conversation = null;
            return;
        }

        // JMS v186 CP_UserScriptMessageAnswer: [nMsgType:1][action:1][payload by type]
        int messageType = packet.ReadByte();
        int action = (sbyte)packet.ReadByte();
        int selection = -1;
        string text = string.Empty;

        // Only a positive action carries a payload; escape (0xFF/-1) and plain-end (0) do not.
        // Guard every read against the packet's remaining length so a short/hand-crafted answer
        // ends the conversation instead of crash-disconnecting the session.
        if (action > 0)
        {
            switch (messageType)
            {
                case 5:  // SM_ASKMENU
                    if (packet.Remaining >= 4) { selection = packet.ReadInt(); }
                    break;
                case 3:  // SM_ASKTEXT
                    if (packet.Remaining >= 2) { text = packet.ReadString(); }
                    break;
                case 8:  // SM_ASKAVATAR
                    if (packet.Remaining >= 1) { selection = packet.ReadByte(); }
                    break;
                case 15: // SM_ASKSLIDEMENU
                    if (packet.Remaining >= 4) { selection = packet.ReadInt(); }
                    break;
            }
        }

        conversation.Advance(messageType, action, selection, text);
    }

    private async ValueTask HandleTransferFieldAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || _field is null)
        {
            return;
        }

        // A dead player dismissing the tombstone dialog sends a transfer request; revive them at
        // this map's return town (or in place) with full HP/MP instead of a normal transfer.
        if (_player.Character.Hp <= 0)
        {
            await ReviveAsync(session).ConfigureAwait(false);
            return;
        }

        // JMS v186 CP_UserTransferFieldRequest:
        //   [portalCount:1][mapId:4][portalName:str][x:2,y:2 if portal][unk:1][reviveType:1]
        packet.ReadByte();
        int targetMapId = packet.ReadInt();
        string portalName = packet.ReadString();

        // A direct map id (portal name empty) is a /map-style jump: honor it as-is.
        if (string.IsNullOrEmpty(portalName))
        {
            if (targetMapId < 0)
            {
                await session.SendAsync(_packets.TransferFieldReqIgnored(TransferDisabledPortal)).ConfigureAwait(false);
                return;
            }

            await MovePlayerToMapAsync(session, targetMapId, spawnPortal: 0).ConfigureAwait(false);
            return;
        }

        // Portal-by-name: look up the portal on the current map and follow its link.
        MapData? currentMap = _maps.GetMap(_player.Character.MapId);
        PortalData? portal = currentMap?.FindPortal(portalName);
        if (portal is null || !portal.LinksToMap)
        {
            await session.SendAsync(_packets.TransferFieldReqIgnored(TransferDisabledPortal)).ConfigureAwait(false);
            return;
        }

        int spawn = ResolveSpawnPortal(portal.TargetMapId, portal.TargetName);
        await MovePlayerToMapAsync(session, portal.TargetMapId, spawn).ConfigureAwait(false);
    }

    /// <summary>Finds the spawn portal id in the destination map by its target-portal name.</summary>
    private int ResolveSpawnPortal(int targetMapId, string targetPortalName)
    {
        MapData? target = _maps.GetMap(targetMapId);
        PortalData? spawn = string.IsNullOrEmpty(targetPortalName)
            ? target?.SpawnPortal
            : target?.FindPortal(targetPortalName) ?? target?.SpawnPortal;
        return spawn?.Id ?? 0;
    }

    /// <summary>
    /// Revives a dead player: restores full HP/MP, then transfers to this map's return town (or
    /// the same map when it has none), which clears the client's death state.
    /// </summary>
    private async ValueTask ReviveAsync(MapleSession session)
    {
        Character c = _player!.Character;
        c.Hp = c.MaxHp;
        c.Mp = c.MaxMp;

        int reviveMap = _maps.GetMap(c.MapId)?.ReviveMap ?? c.MapId;
        await MovePlayerToMapAsync(session, reviveMap, spawnPortal: 0).ConfigureAwait(false);
        await NotifyPartyOfMyHpAsync(_player!).ConfigureAwait(false); // party sees the revive
    }

    // The death exp penalty itself is applied at the moment of death
    // (CharacterProgression.ApplyDeathPenalty in HandleUserHitAsync), not here.

    /// <summary>
    /// Moves the bound player to another map: leave + announce, switch fields, SetField
    /// (map-change branch), then exchange enter-field packets in the new map.
    /// </summary>
    private async ValueTask MovePlayerToMapAsync(MapleSession session, int targetMapId, int spawnPortal)
    {
        FieldPlayer player = _player!;
        Field oldField = _field!;

        // Summons don't cross maps (a documented simplification — the reference re-spawns them).
        foreach (FieldSummon summon in oldField.RemoveSummonsOf(player.Character.Id))
        {
            await oldField.BroadcastAsync(_packets.SummonedLeaveField(summon, animated: false)).ConfigureAwait(false);
        }

        oldField.Leave(player.Character.Id);
        await oldField.BroadcastAsync(_packets.UserLeaveField(player.Character.Id)).ConfigureAwait(false);
        await ReleaseControlledMobsAsync(oldField, player.Character.Id).ConfigureAwait(false);

        player.Character.MapId = targetMapId;
        player.Character.Portal = (byte)spawnPortal;
        _characters.Save(player.Character); // DB-backed repos need an explicit flush

        await session.SendAsync(_packets.SetFieldChangeMap(player.Character, _channelId)).ConfigureAwait(false);

        Field newField = _fields.Get(targetMapId);
        foreach (FieldPlayer other in newField.Players)
        {
            await session.SendAsync(_packets.UserEnterField(other, GuildOf(other.Character))).ConfigureAwait(false);
        }

        newField.Enter(player);
        _field = newField;
        await newField.BroadcastAsync(_packets.UserEnterField(player, GuildOf(player.Character)), exceptCharacterId: player.Character.Id)
            .ConfigureAwait(false);

        await SpawnReactorsAsync(session, newField).ConfigureAwait(false);

        // The pet follows its owner through the portal (ports the transfer-field respawn).
        if (player.Pet is { } pet)
        {
            pet.X = player.X;
            pet.Y = player.Y;
            await newField.BroadcastAsync(_packets.PetActivated(player.Character.Id, pet, transferField: true)).ConfigureAwait(false);
        }

        // Open game rooms and shops in the new map show their balloons.
        foreach (MiniGame game in _miniGames.GamesInMap(targetMapId))
        {
            await session.SendAsync(_packets.MiniRoomBalloon(game.Owner.Character.Id, game)).ConfigureAwait(false);
        }

        foreach (PlayerShop shop in _playerShops.ShopsInMap(targetMapId))
        {
            await session.SendAsync(_packets.PlayerShopBalloon(shop.Owner.Character.Id, shop)).ConfigureAwait(false);
        }

        foreach (HiredMerchant merchant in _merchants.MerchantsInMap(targetMapId))
        {
            await session.SendAsync(_packets.EmployeeEnterField(merchant)).ConfigureAwait(false);
        }

        await SpawnNpcsAsync(session, newField).ConfigureAwait(false);
        await RefreshPartyWindowAsync(player).ConfigureAwait(false); // party window shows the new map
    }

    /// <summary>
    /// Extracts the start position from a CMovePath buffer:
    /// <c>[startX:2][startY:2]...</c> (CMovePath::Decode reads the head as the origin point).
    /// </summary>
    private static void UpdatePositionFromMovePath(FieldPlayer player, byte[] movePath)
    {
        if (movePath.Length < 4)
        {
            return;
        }

        player.X = (short)(movePath[0] | (movePath[1] << 8));
        player.Y = (short)(movePath[2] | (movePath[3] << 8));
    }

    private static int RandomSeed() => RandomNumberGenerator.GetInt32(int.MaxValue);
}
