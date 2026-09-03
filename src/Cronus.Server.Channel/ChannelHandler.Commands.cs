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
    /// The GM/debug command dispatcher (chat lines starting with '/'). Replies are echoed back to
    /// the caller as their own chat line, one packet per line. Every command's name, usage, and
    /// help text lives in <see cref="CommandTable"/>, which drives /help and the argument-error
    /// replies: a case guard that rejects its arguments falls through to <c>default</c>, which
    /// answers with the registered usage. Adding a case means adding a table entry too.
    /// Documented in docs/COMMANDS.md (Japanese: docs/COMMANDS.ja.md) — keep those in sync.
    /// </summary>
    private async ValueTask HandleCommandAsync(MapleSession session, string command)
    {
        string[] parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return;
        }

        string typed = parts[0];
        string name = typed.ToLowerInvariant();

        // The stat family collapsed into /status, but the old top-level spellings stay usable:
        // rewrite "/hp 500" to "/status hp 500" so the behaviour lives in exactly one place.
        if (CommandTable.IsStatField(name))
        {
            parts = parts.Prepend("status").ToArray();
            name = "status";
        }

        switch (name)
        {
            case "status":
                await HandleStatusAsync(session, parts).ConfigureAwait(false);
                break;

            case "map" when parts.Length >= 2: // legacy spelling, same behaviour
            case "warp" when parts.Length >= 2:
            {
                // One warp command for both addressing modes: a number is a map id, anything else
                // is an online player whose map we jump to.
                if (int.TryParse(parts[1], out int warpMapId))
                {
                    await MovePlayerToMapAsync(session, warpMapId, spawnPortal: 0).ConfigureAwait(false);
                    break;
                }

                FieldPlayer? warpTarget = _fields.FindPlayerByName(parts[1]);
                if (warpTarget is null || warpTarget.Character.Id == _player!.Character.Id)
                {
                    await ReplyAsync(session, $"'{parts[1]}' はオンラインではありません").ConfigureAwait(false);
                    break;
                }

                await MovePlayerToMapAsync(session, warpTarget.Character.MapId, spawnPortal: 0).ConfigureAwait(false);
                break;
            }

            case "dbgwarp":
            {
                // A windowed warp console: pick a region, pick a map, go — no ids to type. Same
                // dialog plumbing as /beauty and /dbgshop.
                if (_conversation is { IsEnded: false })
                {
                    break; // another dialog is open
                }

                if (_mapCatalog is null || _mapCatalog.Regions.Count == 0)
                {
                    await ReplyAsync(session, "マップカタログが未ロードです (CRONUS_WZ)").ConfigureAwait(false);
                    break;
                }

                var warpDialog = new ChannelNpcDialog(session, _packets);
                var warpConvo = new NpcConversation(BeautyNpcId, warpDialog);
                _conversation = warpConvo;
                IMapCatalog warpCatalog = _mapCatalog;
                var warpThread = new Thread(() => RunDebugWarpFlow(warpConvo, warpCatalog, session))
                {
                    IsBackground = true,
                    Name = "dbgwarp-console",
                };
                warpThread.Start();
                break;
            }

            case "pos":
                await ReplyAsync(session, $"pos: ({_player!.X}, {_player.Y}) map {_player.Character.MapId}")
                    .ConfigureAwait(false);
                break;

            case "notice" when parts.Length >= 2:
            {
                // /notice <msg> is this map; /notice all <msg> is every map on every channel.
                bool everywhere = parts[1].Equals("all", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3;
                byte[] notice = _packets.BroadcastNotice(string.Join(' ', parts.Skip(everywhere ? 2 : 1)));
                if (everywhere)
                {
                    foreach (Field f in _fields.Fields)
                    {
                        await f.BroadcastAsync(notice).ConfigureAwait(false);
                    }
                }
                else
                {
                    await _field!.BroadcastAsync(notice).ConfigureAwait(false);
                }

                break;
            }

            case "snotice" when parts.Length >= 2:
            {
                // Legacy spelling of "/notice all".
                byte[] notice = _packets.BroadcastNotice(string.Join(' ', parts.Skip(1)));
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

            case "dbgshop":
            case "shop" when parts.Length < 2:
            {
                // A debug shop stocking EVERY item in the game at 1 meso, browsed like /beauty:
                // pick a category, pick a page, and the shop window opens with that page's stock.
                if (_conversation is { IsEnded: false })
                {
                    break; // another dialog is open
                }

                if (_itemCatalog is null || _itemCatalog.Categories.Count == 0)
                {
                    await ReplyAsync(session, "アイテムカタログが未ロードです (CRONUS_WZ)").ConfigureAwait(false);
                    break;
                }

                var shopDialog = new ChannelNpcDialog(session, _packets);
                var shopConvo = new NpcConversation(BeautyNpcId, shopDialog);
                _conversation = shopConvo;
                IItemCatalog catalog = _itemCatalog;
                var shopThread = new Thread(() => RunDebugShopFlow(shopConvo, catalog, session))
                {
                    IsBackground = true,
                    Name = "dbgshop-console",
                };
                shopThread.Start();
                break;
            }

            case "shop" when int.TryParse(parts[1], out int shopId):
            {
                Shop? shop = _shops.GetShop(shopId);
                if (shop is null)
                {
                    await ReplyAsync(session, $"ショップ {shopId} は存在しません").ConfigureAwait(false);
                    break;
                }

                await OpenShopAsync(session, shop).ConfigureAwait(false);
                break;
            }

            case "storage":
                await OpenStorageAsync(session).ConfigureAwait(false);
                break;

            case "clear" when parts.Length >= 2:
                await HandleClearAsync(session, parts).ConfigureAwait(false);
                break;

            case "clearinv":
                await ClearInventoryAsync(session, parts.Length >= 2 ? parts[1] : null).ConfigureAwait(false);
                break;

            case "questreset" when parts.Length >= 2 && int.TryParse(parts[1], out int legacyQuestId):
                await ClearQuestAsync(session, legacyQuestId).ConfigureAwait(false);
                break;

            case "guildcreate" when parts.Length >= 2:
                // Free, works anywhere (the client's own flow needs the HQ map and 5m meso).
                await CreateGuildAsync(session, _player!.Character, parts[1], cost: 0).ConfigureAwait(false);
                break;

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

            case "save":
                _characters.Save(_player!.Character);
                await ReplyAsync(session, "saved").ConfigureAwait(false);
                break;

            case "help":
            {
                if (parts.Length >= 2 && CommandTable.TryGet(parts[1].TrimStart('/'), out CommandSpec detail))
                {
                    await ReplyLinesAsync(session, CommandTable.DetailLines(detail)).ConfigureAwait(false);
                    break;
                }

                if (parts.Length >= 2)
                {
                    await ReplyAsync(session, $"不明なコマンド: {parts[1]}").ConfigureAwait(false);
                }

                await ReplyLinesAsync(session, CommandTable.HelpLines()).ConfigureAwait(false);
                break;
            }

            default:
            {
                // A registered name reaching here means the case guard rejected the arguments —
                // answer with how the command is meant to be typed rather than "unknown command".
                if (CommandTable.TryGet(name, out CommandSpec spec))
                {
                    await ReplyAsync(session, "引数が正しくありません。").ConfigureAwait(false);
                    await ReplyAsync(session, "使い方: " + spec.Usage).ConfigureAwait(false);
                    break;
                }

                string? suggestion = CommandTable.Suggest(name);
                await ReplyAsync(session, $"不明なコマンド: /{typed}"
                    + (suggestion is null ? string.Empty : $" — もしかして /{suggestion} ?")).ConfigureAwait(false);
                await ReplyAsync(session, "/help でコマンド一覧を表示します").ConfigureAwait(false);
                break;
            }
        }
    }

    /// <summary>
    /// <c>/status</c> — the consolidated stat command. With no field it prints the caller's stat
    /// sheet; with a field and a value it applies the change (the same behaviours the old per-stat
    /// commands had, which remain as aliases).
    /// </summary>
    private async ValueTask HandleStatusAsync(MapleSession session, string[] parts)
    {
        Character c = _player!.Character;
        if (parts.Length < 2)
        {
            await ReplyLinesAsync(session, new[]
            {
                $"── {c.Name} ── Lv.{c.Level} job {c.Job} exp {c.Exp}",
                $"HP {c.Hp}/{c.MaxHp}   MP {c.Mp}/{c.MaxMp}",
                $"STR {c.Str}  DEX {c.Dex}  INT {c.Int}  LUK {c.Luk}",
                $"AP {c.Ap}  SP {c.Sp}  fame {c.Fame}  meso {c.Meso}",
                "変更するには /status <項目> <値>",
            }).ConfigureAwait(false);
            return;
        }

        string field = parts[1].ToLowerInvariant();
        if (!CommandTable.IsStatField(field))
        {
            await ReplyAsync(session, $"不明な項目: {parts[1]}").ConfigureAwait(false);
            await ReplyAsync(session, "項目: " + string.Join(" ", CommandTable.StatFields)).ConfigureAwait(false);
            return;
        }

        if (parts.Length < 3 || !int.TryParse(parts[2], out int value))
        {
            await ReplyAsync(session, $"使い方: /status {field} <値>").ConfigureAwait(false);
            return;
        }

        await ApplyStatAsync(session, field, value).ConfigureAwait(false);
    }

    /// <summary>Applies one <c>/status</c> field change and reports the resulting value.</summary>
    private async ValueTask ApplyStatAsync(MapleSession session, string field, int value)
    {
        Character c = _player!.Character;
        switch (field)
        {
            case "level":
            {
                int target = Math.Clamp(value, 1, 200);
                StatFlag levelChanged = StatFlag.Level | StatFlag.Exp;
                if (target > c.Level)
                {
                    // Raising runs real level-ups so HP/MP/AP/SP grow like normal play.
                    levelChanged |= CharacterProgression.ForceLevelUps(c, target - c.Level, EffectResolverFor(c));
                }
                else
                {
                    c.Level = (byte)target; // lowering just sets the level (stats keep their values)
                }

                c.Exp = 0; // reset so the new level's bar starts clean
                _characters.Save(c);
                await session.SendAsync(_packets.StatChanged(c, levelChanged)).ConfigureAwait(false);
                await RefreshPartyWindowAsync(_player).ConfigureAwait(false); // party window shows levels
                if (c.GuildId > 0)
                {
                    await BroadcastToGuildAsync(c.GuildId, _packets.GuildMemberLevelJob(c.GuildId, c.Id, c.Level, c.Job), exceptCharacterId: c.Id).ConfigureAwait(false);
                }

                break;
            }

            case "job":
                await SetStatAsync(session, StatFlag.Job, ch => ch.Job = (short)value).ConfigureAwait(false);
                break;

            case "exp":
                await SetStatAsync(session, StatFlag.Exp, ch => ch.Exp = Math.Max(0, value)).ConfigureAwait(false);
                break;

            case "hp":
                c.Hp = (short)Math.Clamp(value, 0, c.MaxHp);
                _characters.Save(c);
                await session.SendAsync(_packets.StatChanged(c, StatFlag.Hp)).ConfigureAwait(false);
                await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false);
                break;

            case "maxhp":
                c.MaxHp = (short)Math.Clamp(value, 1, 30000);
                c.Hp = Math.Min(c.Hp, c.MaxHp);
                _characters.Save(c);
                await session.SendAsync(_packets.StatChanged(c, StatFlag.Hp | StatFlag.MaxHp)).ConfigureAwait(false);
                await NotifyPartyOfMyHpAsync(_player).ConfigureAwait(false);
                break;

            case "mp":
                await SetStatAsync(session, StatFlag.Mp, ch => ch.Mp = (short)Math.Clamp(value, 0, ch.MaxMp)).ConfigureAwait(false);
                break;

            case "maxmp":
                await SetStatAsync(session, StatFlag.Mp | StatFlag.MaxMp, ch =>
                {
                    ch.MaxMp = (short)Math.Clamp(value, 1, 30000);
                    ch.Mp = Math.Min(ch.Mp, ch.MaxMp);
                }).ConfigureAwait(false);
                break;

            case "str":
                await SetStatAsync(session, StatFlag.Str, ch => ch.Str = (short)Math.Clamp(value, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "dex":
                await SetStatAsync(session, StatFlag.Dex, ch => ch.Dex = (short)Math.Clamp(value, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "int":
                await SetStatAsync(session, StatFlag.Int, ch => ch.Int = (short)Math.Clamp(value, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "luk":
                await SetStatAsync(session, StatFlag.Luk, ch => ch.Luk = (short)Math.Clamp(value, 4, short.MaxValue)).ConfigureAwait(false);
                break;

            case "ap": // additive, like the original /ap
                await SetStatAsync(session, StatFlag.Ap, ch => ch.Ap = (short)Math.Clamp(ch.Ap + value, 0, short.MaxValue)).ConfigureAwait(false);
                break;

            case "sp": // additive, like the original /sp
                await SetStatAsync(session, StatFlag.Sp, ch => ch.Sp = (short)Math.Clamp(ch.Sp + value, 0, short.MaxValue)).ConfigureAwait(false);
                break;

            case "fame":
                await SetStatAsync(session, StatFlag.Fame, ch => ch.Fame = (short)Math.Clamp(value, -30000, 30000)).ConfigureAwait(false);
                break;

            case "meso": // additive, like the original /meso
                await SetStatAsync(session, StatFlag.Meso, ch => ch.Meso = (int)Math.Clamp((long)ch.Meso + value, 0, int.MaxValue)).ConfigureAwait(false);
                break;
        }

        await ReplyAsync(session, $"{field} → {CurrentStat(c, field)}").ConfigureAwait(false);
    }

    /// <summary>The current value of one <c>/status</c> field, for the confirmation line.</summary>
    private static string CurrentStat(Character c, string field) => field switch
    {
        "level" => c.Level.ToString(),
        "job" => c.Job.ToString(),
        "exp" => c.Exp.ToString(),
        "hp" => $"{c.Hp}/{c.MaxHp}",
        "maxhp" => c.MaxHp.ToString(),
        "mp" => $"{c.Mp}/{c.MaxMp}",
        "maxmp" => c.MaxMp.ToString(),
        "str" => c.Str.ToString(),
        "dex" => c.Dex.ToString(),
        "int" => c.Int.ToString(),
        "luk" => c.Luk.ToString(),
        "ap" => c.Ap.ToString(),
        "sp" => c.Sp.ToString(),
        "fame" => c.Fame.ToString(),
        "meso" => c.Meso.ToString(),
        _ => "?",
    };

    /// <summary><c>/clear</c> — the consolidated "wipe some record" command.</summary>
    private async ValueTask HandleClearAsync(MapleSession session, string[] parts)
    {
        switch (parts[1].ToLowerInvariant())
        {
            case "inv":
            case "inventory":
                await ClearInventoryAsync(session, parts.Length >= 3 ? parts[2] : null).ConfigureAwait(false);
                break;

            case "quest" when parts.Length >= 3 && int.TryParse(parts[2], out int questId):
                await ClearQuestAsync(session, questId).ConfigureAwait(false);
                break;

            case "book":
            case "monsterbook":
                _player!.Character.MonsterCards.Clear();
                _characters.Save(_player.Character);
                await ReplyAsync(session, "モンスターブックを消去しました — カードが再びドロップします").ConfigureAwait(false);
                break;

            default:
                await ReplyAsync(session, "使い方: /clear <inv [タブ]|quest <クエストID>|book>").ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Empties inventory tabs (positive slots only — worn equips stay): no tab wipes all five,
    /// a tab number (1-5) just that one. Sends the per-slot removes so the grid clears live.
    /// </summary>
    private async ValueTask ClearInventoryAsync(MapleSession session, string? tabArg)
    {
        Character cc = _player!.Character;
        int? onlyTab = tabArg is not null && int.TryParse(tabArg, out int t) && t is >= 1 and <= 5 ? t : null;
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
    }

    /// <summary>Clears one quest from both records (debug/bot use: makes quest flows re-runnable).</summary>
    private async ValueTask ClearQuestAsync(MapleSession session, int questId)
    {
        Character qc = _player!.Character;
        bool removed = qc.StartedQuests.Remove(questId) | qc.CompletedQuests.Remove(questId);
        _characters.Save(qc);
        if (removed)
        {
            await session.SendAsync(_packets.QuestRecordMessage(questId, ChannelPackets.QuestRecordNone)).ConfigureAwait(false);
        }

        await ReplyAsync(session, $"quest {questId} reset").ConfigureAwait(false);
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

    /// <summary>
    /// Sends several chat lines in order. The client renders each chat packet as its own row, so
    /// this is how multi-line output (/help, /status) gets its line breaks.
    /// </summary>
    private async ValueTask ReplyLinesAsync(MapleSession session, IEnumerable<string> lines)
    {
        foreach (string line in lines)
        {
            await ReplyAsync(session, line).ConfigureAwait(false);
        }
    }

    private async ValueTask HandleSelectNpcAsync(MapleSession session, PacketReader packet)
    {
        // One conversation at a time; ignore a new NPC while a script is still running — and
        // while a shop or the storage window is open (the oracle holds its conversation lock
        // for those too: OnUserSelectNpc bails when getConversation() != 0, which sendShop and
        // trunk set. A dialog opening over a live shop window desyncs the client's UI state).
        if (_player is null || _conversation is { IsEnded: false } || _openShop is not null || _openStorage is not null)
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

        if (_npcScripts is not null)
        {
            var dialog = new ChannelNpcDialog(session, _packets);
            _conversation = _npcScripts.Start(templateId, dialog, CreateScriptPlayer(session));
            if (_conversation is not null)
            {
                return;
            }
        }

        // No script and no shop. For a QUEST NPC, send NOTHING (ports OnUserSelectNpc —
        // TacosScriptNPC.start just returns false): the client then runs its own quest UI, and a
        // server-sent dialog here would swallow the click and break the conversation. An NPC with
        // no quests either gets a small fallback line instead — many of the v186 map spawns are
        // long-expired event NPCs whose server scripts (wz info/script names like
        // "visitor_gogocube") only ever existed on Nexon's side, and dead-silent clicks read as
        // a bug to players.
        if (_questNpcs is null || _questNpcs.HasQuests(templateId))
        {
            return;
        }

        var fallbackDialog = new ChannelNpcDialog(session, _packets);
        var fallbackConvo = new NpcConversation(templateId, fallbackDialog);
        _conversation = fallbackConvo;
        var fallbackThread = new Thread(() => RunNpcFallback(fallbackConvo))
        {
            IsBackground = true,
            Name = $"npc-fallback-{templateId}",
        };
        fallbackThread.Start();
    }

    /// <summary>One flavor line for a script-less, shop-less, quest-less NPC, then done.</summary>
    private static void RunNpcFallback(NpcConversation cm)
    {
        try
        {
            cm.sendOk("（今は特に話すことはないようだ……）");
        }
        catch (ConversationEndedException)
        {
            // the player closed the window — fine
        }
        finally
        {
            cm.End();
        }
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

    /// <summary>Items offered per /dbgshop page (the shop window scrolls, but keep packets sane).</summary>
    private const int DebugShopPageSize = 200;

    /// <summary>Every /dbgshop item costs this much — cheap enough to buy anything, non-zero so
    /// the client's own "can I afford it" check behaves normally.</summary>
    private const int DebugShopPrice = 1;

    /// <summary>
    /// The /dbgshop conversation: category menu → page menu (categories run to thousands of
    /// items) → a synthetic shop stocking that page at 1 meso each. Runs on its own thread and
    /// blocks on the client's answers, like the NPC scripts and /beauty.
    /// </summary>
    private void RunDebugShopFlow(NpcConversation cm, IItemCatalog catalog, MapleSession session)
    {
        try
        {
            IReadOnlyList<ItemCategory> categories = catalog.Categories;
            var menu = new System.Text.StringBuilder("デバッグショップ（全アイテム " + DebugShopPrice + " メル）\r\nジャンルを選んでください:");
            for (int i = 0; i < categories.Count; i++)
            {
                menu.Append("\r\n#L").Append(i).Append('#')
                    .Append(categories[i].DisplayName)
                    .Append(" （").Append(categories[i].ItemIds.Count).Append("種）#l");
            }

            int pick = cm.askMenu(menu.ToString());
            if (pick < 0 || pick >= categories.Count)
            {
                return;
            }

            ItemCategory category = categories[pick];
            int page = 0;
            int pages = (category.ItemIds.Count + DebugShopPageSize - 1) / DebugShopPageSize;
            if (pages > 1)
            {
                var pageMenu = new System.Text.StringBuilder(category.DisplayName + " — ページを選んでください:");
                for (int i = 0; i < pages; i++)
                {
                    int from = i * DebugShopPageSize + 1;
                    int to = Math.Min(category.ItemIds.Count, (i + 1) * DebugShopPageSize);
                    pageMenu.Append("\r\n#L").Append(i).Append('#').Append(from).Append('-').Append(to).Append("番目#l");
                }

                page = cm.askMenu(pageMenu.ToString());
                if (page < 0 || page >= pages)
                {
                    return;
                }
            }

            var stock = category.ItemIds
                .Skip(page * DebugShopPageSize)
                .Take(DebugShopPageSize)
                .Select((id, index) => new ShopItem(id, DebugShopPrice, index, ReqItem: 0, ReqItemQ: 0))
                .ToList();

            var shop = new Shop { ShopId = DebugShopId, NpcId = BeautyNpcId, Items = stock };

            // The shop window replaces the dialog, so close the conversation first.
            cm.End();
            OpenShopAsync(session, shop).AsTask().GetAwaiter().GetResult();
        }
        catch (ConversationEndedException)
        {
            // The player escaped the dialog — normal end.
        }
        catch (Exception)
        {
            // Never let a browse bug take the session down; the dialog just closes.
        }
        finally
        {
            cm.End();
        }
    }

    /// <summary>Shop id used for the synthetic /dbgshop stock (never collides with wz shops).</summary>
    private const int DebugShopId = -1;

    /// <summary>How many entries one /dbgwarp menu page lists before it splits into pages.</summary>
    private const int DebugWarpPageSize = 20;

    /// <summary>
    /// The /dbgwarp console: region → street → map, then warp. Runs on its own thread because the
    /// dialog helpers block waiting for the player's selection, exactly like an NPC script.
    /// Streets are the middle level because a region can hold over a thousand maps while a street
    /// is usually a handful — and streets ("ヘネシス", "オルビス") are how the game names places.
    /// </summary>
    private void RunDebugWarpFlow(NpcConversation cm, IMapCatalog catalog, MapleSession session)
    {
        try
        {
            IReadOnlyList<MapRegion> regions = catalog.Regions;
            int regionPick = PickFromMenu(
                cm,
                "デバッグワープ\r\n地域を選んでください:",
                regions.Select(r => $"{r.DisplayName} （{r.MapCount}箇所）").ToList());
            if (regionPick < 0)
            {
                return;
            }

            MapRegion region = regions[regionPick];
            int streetPick = PickFromMenu(
                cm,
                region.DisplayName + "\r\nエリアを選んでください:",
                region.Streets.Select(s => $"{s.Name} （{s.Maps.Count}箇所）").ToList());
            if (streetPick < 0)
            {
                return;
            }

            MapStreet street = region.Streets[streetPick];
            int mapPick = PickFromMenu(
                cm,
                street.Name + "\r\n行き先を選んでください:",
                street.Maps.Select(m => $"{m.MapName} （{m.MapId}）").ToList());
            if (mapPick < 0)
            {
                return;
            }

            // The field change tears down the dialog, so close the conversation first.
            int destination = street.Maps[mapPick].MapId;
            cm.End();
            MovePlayerToMapAsync(session, destination, spawnPortal: 0).AsTask().GetAwaiter().GetResult();
        }
        catch (ConversationEndedException)
        {
            // The player escaped the dialog — normal end.
        }
        catch (Exception)
        {
            // Never let a browse bug take the session down; the dialog just closes.
        }
        finally
        {
            cm.End();
        }
    }

    /// <summary>
    /// Shows <paramref name="labels"/> as a selectable menu and returns the chosen index, or -1 if
    /// the player backed out. Lists longer than a page get a "which page" menu first, so no single
    /// dialog grows past what the client can show.
    /// </summary>
    private static int PickFromMenu(NpcConversation cm, string prompt, IReadOnlyList<string> labels)
    {
        if (labels.Count == 0)
        {
            return -1;
        }

        int offset = 0;
        if (labels.Count > DebugWarpPageSize)
        {
            int pages = (labels.Count + DebugWarpPageSize - 1) / DebugWarpPageSize;
            var pageMenu = new System.Text.StringBuilder(prompt).Append("\r\nページを選んでください:");
            for (int i = 0; i < pages; i++)
            {
                // Label each page with its first and last entry so the list stays navigable.
                int last = Math.Min(labels.Count, (i + 1) * DebugWarpPageSize) - 1;
                pageMenu.Append("\r\n#L").Append(i).Append('#')
                    .Append(labels[i * DebugWarpPageSize]).Append(" 〜 ").Append(labels[last]).Append("#l");
            }

            int page = cm.askMenu(pageMenu.ToString());
            if (page < 0 || page >= pages)
            {
                return -1;
            }

            offset = page * DebugWarpPageSize;
        }

        var menu = new System.Text.StringBuilder(prompt);
        int count = Math.Min(DebugWarpPageSize, labels.Count - offset);
        for (int i = 0; i < count; i++)
        {
            menu.Append("\r\n#L").Append(i).Append('#').Append(labels[offset + i]).Append("#l");
        }

        int pick = cm.askMenu(menu.ToString());
        return pick < 0 || pick >= count ? -1 : offset + pick;
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

    /// <summary>
    /// Handles <c>CP_CONTISTATE</c> — the client asks for the ship's state on entering a station or
    /// flight map (ports <c>ReqCField.OnContiState</c> verbatim): every station answers "docked"
    /// (<c>CONTI_WAIT</c>), the two flight maps answer "in flight, mobs incoming"
    /// (<c>CONTI_TARGET_MOVEFIELD</c> + <c>CONTI_MOBGEN</c>). The oracle sends no mob with MOBGEN;
    /// spawning the Crimson Balrog here is our addition (once per empty flight map).
    /// </summary>
    private async ValueTask HandleContiStateAsync(MapleSession session, PacketReader packet)
    {
        if (_player is null || packet.Remaining < 4)
        {
            return;
        }

        packet.ReadInt();                                  // the map id the client thinks it's in
        int mapId = _player.Character.MapId;               // trust our own record
        switch (mapId)
        {
            case 104020110:                                // Ellinia station (post-BB layout)
            case 101000300: case 200000111:                // Ellinia <-> Orbis
            case 200000121: case 220000110:                // Orbis <-> Ludibrium
            case 200000151: case 260000100:                // Orbis <-> Ariant
            case 240000110: case 200000131:                // Orbis <-> Leafre
                await session.SendAsync(_packets.ContiState(ChannelPackets.ContiWait)).ConfigureAwait(false);
                break;

            case 200090010:                                // riding to Orbis
            case 200090000:                                // riding to Ellinia
                await session.SendAsync(_packets.ContiMove(ChannelPackets.ContiTargetMoveField, ChannelPackets.ContiMobGen)).ConfigureAwait(false);
                if (_field is not null && !_field.Mobs.Any(m => !m.IsDead))
                {
                    await ScriptSpawnMobAsync(CrimsonBalrogMobId, 1).ConfigureAwait(false);
                }

                break;
        }
    }

    /// <summary>クリムゾンバルログ — the airship raider.</summary>
    private const int CrimsonBalrogMobId = 9300210;

    /// <summary>The <c>player</c> object handed to NPC / quest / portal scripts.</summary>
    private ChannelPlayer CreateScriptPlayer(MapleSession session) => new(
        _player!.Character, _characters, session, _packets,
        warp: (map, portal) => MovePlayerToMapAsync(session, map, portal),
        openShop: shopId => _shops.GetShop(shopId) is { } s ? OpenShopAsync(session, s) : ValueTask.CompletedTask,
        openStorage: () => OpenStorageAsync(session),
        openParcel: () => session.SendAsync(_packets.ParcelOpen(fromNpc: true)),
        parcelCount: () => _parcels?.LoadFor(_player!.Character.Id).Count ?? 0,
        airshipBoarding: () => AirshipSchedule.IsBoarding(DateTime.UtcNow),
        airshipMinutes: () => (int)Math.Ceiling(AirshipSchedule.UntilDeparture(DateTime.UtcNow).TotalMinutes),
        receiveParcels: async () => (await ReceiveParcelsAsync(session).ConfigureAwait(false)).Delivered,
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
        if (_player is null || _field is null)
        {
            return;
        }

        if (_portalScripts is null)
        {
            await session.SendAsync(_packets.TransferFieldReqIgnored(TransferDisabledPortal)).ConfigureAwait(false);
            return;
        }

        // The oracle plays the portal sound before the script runs (EffectLocal PlayPortalSE).
        await session.SendAsync(_packets.UserEffectLocal(ChannelPackets.UserEffectPlayPortalSE)).ConfigureAwait(false);

        // JMS v186 CP_UserPortalScriptRequest: [portalCount:1][portalName:str][x:2][y:2]
        packet.ReadByte();
        string portalName = packet.ReadString();

        PortalData? portal = _maps.GetMap(_player.Character.MapId)?.FindPortal(portalName);
        if (portal is null || !portal.HasScript)
        {
            // The request locks the client until a SetField or this refusal arrives — the oracle
            // answers every failed portal-script request with TransferFieldReqIgnored.
            await session.SendAsync(_packets.TransferFieldReqIgnored(TransferDisabledPortal)).ConfigureAwait(false);
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

        // JMS v186 CP_UserTransferFieldRequest:
        //   [portalCount:1][mapId:4][portalName:str][x:2,y:2 if portal][unk:1][reviveType:1]
        packet.ReadByte();
        int targetMapId = packet.ReadInt();
        string portalName = packet.ReadString();

        // A dead player dismissing the tombstone dialog sends mapId 0 with no portal; revive_type
        // trails the packet (ports mapChangePortal: > 0 = revive where they died, else the return
        // town) — an alive player's packet is a normal transfer.
        if (_player.Character.Hp <= 0)
        {
            if (portalName.Length > 0 && packet.Remaining >= 4)
            {
                packet.ReadShort();
                packet.ReadShort(); // the x/y that ride portal-form packets
            }

            packet.ReadByte();      // unk
            bool inPlace = packet.Remaining >= 1 && packet.ReadByte() > 0;
            await ReviveAsync(session, inPlace).ConfigureAwait(false);
            return;
        }

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
    private async ValueTask ReviveAsync(MapleSession session, bool inPlace = false)
    {
        Character c = _player!.Character;
        c.Hp = c.MaxHp;
        c.Mp = c.MaxMp;

        int reviveMap = inPlace ? c.MapId : _maps.GetMap(c.MapId)?.ReviveMap ?? c.MapId;
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

        // A map change tears down any open window client-side; drop the matching server state so
        // stale shop/storage/dialog locks can't wedge NPC clicks on the new map (the oracle
        // clears its conversation flag the same way).
        _openShop = null;
        _openStorage = null;
        _conversation?.End();
        _conversation = null;

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
