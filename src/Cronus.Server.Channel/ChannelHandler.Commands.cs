// ChannelHandler partial: GM commands, NPC selection, scripts, map movement.
using System.Globalization;
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
                    // A map this data set does not have would crash the client on SetField — and
                    // again on every re-login, since the map is saved. Refuse it here.
                    if (_maps.KnowsAllMaps && _maps.GetMap(warpMapId) is null)
                    {
                        await ReplyAsync(session, $"マップ {warpMapId} は存在しません（データがありません）").ConfigureAwait(false);
                        break;
                    }

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

            case "gmmove":
            {
                // GM movement mode. Server half: a flag (no damage taken, no skill MP cost, no
                // cooldown). Client half: Speed / Jump / Booster temporary stats. The stock client
                // clamps speed to 140%, jump to 123% and attack speed to degree 2 —
                // DevTools/clientpatch_speedcap.py lifts all three. Multipliers are of the 100%
                // base: /gmmove 3 = 300% speed.
                //   /gmmove                          toggle (defaults: speed 3x, jump 1.8x, attack 1x)
                //   /gmmove <speed> [<jump> [<attack>]]   on, with those multipliers
                //   /gmmove on|off
                string? a1 = parts.Length >= 2 ? parts[1] : null;
                bool? requested = a1 is null ? null
                    : a1.Equals("on", StringComparison.OrdinalIgnoreCase) ? true
                    : a1.Equals("off", StringComparison.OrdinalIgnoreCase) ? false
                    : double.TryParse(a1, NumberStyles.Float, CultureInfo.InvariantCulture, out _) ? true
                    : null;
                if (a1 is not null && requested is null)
                {
                    await ReplyAsync(session, "使い方: /gmmove [on|off|<速度倍率> [<ジャンプ倍率> [<攻撃速度倍率>]]]  例: /gmmove 3 2 4").ConfigureAwait(false);
                    break;
                }

                double speedTimes = a1 is not null && double.TryParse(a1, NumberStyles.Float, CultureInfo.InvariantCulture, out double st) ? st : GmMoveDefaultSpeedTimes;
                double jumpTimes = parts.Length >= 3 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double jt) ? jt : GmMoveDefaultJumpTimes;
                double attackTimes = parts.Length >= 4 && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double at) ? at : GmMoveDefaultAttackTimes;
                if (speedTimes is < 1 or > 100 || jumpTimes is < 1 or > 30 || attackTimes is < 1 or > 8)
                {
                    await ReplyAsync(session, "倍率の範囲: 速度 1〜100、ジャンプ 1〜30、攻撃速度 1〜8").ConfigureAwait(false);
                    break;
                }

                bool on = requested ?? !_player!.GmMove;
                if (_player!.GmMove)
                {
                    // Re-issuing while on (new multipliers): clear the old stats first so the client
                    // replaces rather than stacks them.
                    await session.SendAsync(_packets.TemporaryStatReset(GmMoveMask)).ConfigureAwait(false);
                }

                _player.GmMove = on;
                if (on)
                {
                    int weaponSpeed = EquippedWeaponAttackSpeed(_player.Character);
                    BuffStat[] buffs = GmMoveBuffs(speedTimes, jumpTimes, GmMoveBooster(attackTimes, weaponSpeed));
                    await session.SendAsync(_packets.TemporaryStatSet(buffs)).ConfigureAwait(false);
                    await ReplyAsync(session, $"gmmove: ON — 速度{speedTimes:0.#}倍/ジャンプ{jumpTimes:0.#}倍/攻撃速度{attackTimes:0.#}倍(武器速度{weaponSpeed})、被ダメージ無効、スキルのMP消費・クールタイム無し").ConfigureAwait(false);
                }
                else
                {
                    await ReplyAsync(session, "gmmove: OFF").ConfigureAwait(false);
                }

                break;
            }

            case "talk" when parts.Length >= 2 && int.TryParse(parts[1], out int talkNpcId):
            {
                // /talk <npcId>: start that NPC's script here, wherever the NPC actually stands. The
                // real dialog and the real script player — warps, items, quests all happen. The bot
                // exerciser drives every scripted NPC through this; it is handy by hand too.
                if (_npcScripts is null)
                {
                    await ReplyAsync(session, NpcConversation.DevPrefix + "NPC スクリプトが読み込まれていません").ConfigureAwait(false);
                    break;
                }

                if (_npcNames is not null && !_npcNames.HasImage(talkNpcId))
                {
                    await ReplyAsync(session, $"{NpcConversation.DevPrefix}NPC {talkNpcId} の画像がこのクライアントにありません（会話を開くと落ちます）").ConfigureAwait(false);
                    break;
                }

                if (_conversation is { IsEnded: false })
                {
                    _conversation.End();
                }

                NpcConversation? talk = _npcScripts.Start(talkNpcId, new ChannelNpcDialog(session, _packets), CreateScriptPlayer(session));
                if (talk is null)
                {
                    await ReplyAsync(session, $"talk: NPC {talkNpcId} にはスクリプトがありません").ConfigureAwait(false);
                    break;
                }

                _conversation = talk;
                break;
            }

            case "sweep":
            {
                // Crash inventory without a human list (docs/TASK.md フェーズ0): warp this client
                // through every map the catalog knows, a few seconds apart, logging each map to the
                // console and to sweep-progress.txt BEFORE the warp. When the client crashes, the
                // last line names the map; /sweep resume continues from the one after it.
                //   /sweep maps [from] [to] [seconds]   every map in [from, to] (defaults: all, 3s)
                //   /sweep resume [seconds]             from the map after the last logged one
                //   /sweep stop
                string sub = parts.Length >= 2 ? parts[1].ToLowerInvariant() : string.Empty;
                if (sub == "stop")
                {
                    _sweep?.Cancel();
                    _sweep = null;
                    await ReplyAsync(session, "sweep: 停止しました").ConfigureAwait(false);
                    break;
                }

                if (sub == "npcs")
                {
                    // /sweep npcs [開始NPC] [秒/ページ]: every scripted NPC's dialog, rendered by this client.
                    if (_npcScripts is null)
                    {
                        await ReplyAsync(session, NpcConversation.DevPrefix + "NPC スクリプトが読み込まれていません").ConfigureAwait(false);
                        break;
                    }

                    int fromNpc = parts.Length >= 3 && int.TryParse(parts[2], out int fn) ? fn : 0;
                    double perPage = parts.Length >= 4 && double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double pp) ? pp : 1.5;
                    List<int> npcIds = _npcScripts.ScriptedNpcIds.Where(id => id >= fromNpc).OrderBy(id => id).ToList();
                    if (npcIds.Count == 0)
                    {
                        await ReplyAsync(session, "sweep: 対象の NPC がありません").ConfigureAwait(false);
                        break;
                    }

                    _sweep?.Cancel();
                    var npcCts = new CancellationTokenSource();
                    _sweep = npcCts;
                    await ReplyAsync(session, $"sweep: {npcIds.Count} 体の NPC 会話を {perPage:0.##} 秒/ページで流します。落ちたら {Path.GetFileName(SweepProgressFile)} の最終 npc 行が原因、/sweep npcs <次のID> で続き、/sweep stop で停止").ConfigureAwait(false);
                    _ = RunNpcSweepAsync(session, npcIds, TimeSpan.FromSeconds(Math.Clamp(perPage, 0.2, 30)), npcCts.Token);
                    break;
                }

                if (sub is not ("maps" or "resume"))
                {
                    await ReplyAsync(session, "使い方: /sweep maps [開始ID] [終了ID] [秒]  /sweep resume [秒]  /sweep npcs [開始NPC] [秒/ページ]  /sweep stop").ConfigureAwait(false);
                    break;
                }

                if (_mapCatalog is null || _mapCatalog.Regions.Count == 0)
                {
                    await ReplyAsync(session, NpcConversation.DevPrefix + "マップ一覧がありません（gamedata が必要です）").ConfigureAwait(false);
                    break;
                }

                int from = 0;
                int to = int.MaxValue;
                double seconds = SweepDefaultDwellSeconds;
                if (sub == "maps")
                {
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int f)) from = f;
                    if (parts.Length >= 4 && int.TryParse(parts[3], out int t)) to = t;
                    if (parts.Length >= 5 && double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double sec)) seconds = sec;
                }
                else
                {
                    int? last = LastSweptMapId();
                    if (last is null)
                    {
                        await ReplyAsync(session, "sweep: 前回の記録がありません。/sweep maps から始めてください").ConfigureAwait(false);
                        break;
                    }

                    from = last.Value + 1;
                    if (parts.Length >= 3 && double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double sec)) seconds = sec;
                    await ReplyAsync(session, $"sweep: 前回の最終マップ {last.Value} が容疑者です。その次から再開します").ConfigureAwait(false);
                }

                seconds = Math.Clamp(seconds, 0.05, 60);
                List<MapEntry> queue = _mapCatalog.Regions
                    .SelectMany(r => r.Maps)
                    .Where(m => m.MapId >= from && m.MapId <= to)
                    .GroupBy(m => m.MapId)
                    .Select(g => g.First())
                    .OrderBy(m => m.MapId)
                    .ToList();
                if (queue.Count == 0)
                {
                    await ReplyAsync(session, "sweep: 対象のマップがありません").ConfigureAwait(false);
                    break;
                }

                _sweep?.Cancel();
                var sweepCts = new CancellationTokenSource();
                _sweep = sweepCts;
                await ReplyAsync(session, $"sweep: {queue.Count} マップを {seconds:0.##} 秒間隔で巡回します。落ちたら {Path.GetFileName(SweepProgressFile)} の最終行が原因のマップ、/sweep resume で続き、/sweep stop で停止").ConfigureAwait(false);
                _ = RunSweepAsync(session, queue, TimeSpan.FromSeconds(seconds), sweepCts.Token);
                break;
            }

            case "conti" when parts.Length >= 3:
            {
                // Live bisect for the airship packets the oracle never verified: sends one
                // LP_CONTISTATE / LP_CONTIMOVE with the given bytes to this client only.
                //   /conti state <state> [appearShip]   /conti move <first> <second>
                if (!byte.TryParse(parts[2], out byte a)
                    || (parts.Length >= 4 && !byte.TryParse(parts[3], out _)))
                {
                    await ReplyAsync(session, "使い方: /conti state <状態 0-12> [appear 0/1] | /conti move <値1> <値2>").ConfigureAwait(false);
                    break;
                }

                byte b = parts.Length >= 4 ? byte.Parse(parts[3]) : (byte)0;
                switch (parts[1].ToLowerInvariant())
                {
                    case "state":
                        await session.SendAsync(_packets.ContiState(a, b)).ConfigureAwait(false);
                        await ReplyAsync(session, $"conti: LP_CONTISTATE [{a}][{b}] を送信しました").ConfigureAwait(false);
                        break;
                    case "move":
                        await session.SendAsync(_packets.ContiMove(a, b)).ConfigureAwait(false);
                        await ReplyAsync(session, $"conti: LP_CONTIMOVE [{a}][{b}] を送信しました").ConfigureAwait(false);
                        break;
                    default:
                        await ReplyAsync(session, "使い方: /conti state <状態 0-12> [appear 0/1] | /conti move <値1> <値2>").ConfigureAwait(false);
                        break;
                }

                break;
            }

            case "notice" when parts.Length >= 2:
            {
                // /notice <msg> is this map; /notice all <msg> is every map on every channel.
                bool everywhere = parts[1].Equals("all", StringComparison.OrdinalIgnoreCase) && parts.Length >= 3;
                byte[] notice = _packets.BroadcastNotice(string.Join(' ', parts.Skip(everywhere ? 2 : 1)));
                if (everywhere)
                {
                    await _world.BroadcastAsync(notice).ConfigureAwait(false); // every channel of every Channel process
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
                await _world.BroadcastAsync(notice).ConfigureAwait(false);

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

                // A same-channel migration goes through the World like any other, so the
                // re-entry is admitted (and the character stays "online here" in between).
                System.Net.IPEndPoint? self = await _world.MigrateOutAsync(gc.AccountId, gc.Id, _channelId, Cronus.Server.Core.MigrationSource.Channel).ConfigureAwait(false);
                if (self is not null)
                {
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

    // ----- /sweep: automated crash inventory ------------------------------------------------

    private const double SweepDefaultDwellSeconds = 3;

    /// <summary>Where the sweep logs each map before warping to it (next to the host executable).</summary>
    public static string SweepProgressFile => Path.Combine(AppContext.BaseDirectory, "sweep-progress.txt");

    private static readonly object SweepFileLock = new();

    /// <summary>The map id on the last non-comment line of the progress file, if any.</summary>
    private static int? LastSweptMapId()
    {
        lock (SweepFileLock)
        {
            if (!File.Exists(SweepProgressFile))
            {
                return null;
            }

            foreach (string line in File.ReadAllLines(SweepProgressFile).Reverse())
            {
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                string head = line.Split('\t')[0];
                if (int.TryParse(head, out int id))
                {
                    return id;
                }
            }

            return null;
        }
    }

    private static void AppendSweepLine(string line)
    {
        lock (SweepFileLock)
        {
            File.AppendAllText(SweepProgressFile, line + Environment.NewLine);
        }
    }

    /// <summary>
    /// The sweep loop: log, warp, wait, repeat. It runs beside the session's packet loop the same
    /// way the airship scheduler does (through MovePlayerToMapAsync). A crash ends the session,
    /// which cancels the token from OnDisconnectedAsync — the last logged map is the suspect.
    /// </summary>
    private async Task RunSweepAsync(MapleSession session, List<MapEntry> maps, TimeSpan dwell, CancellationToken ct)
    {
        try
        {
            AppendSweepLine($"# sweep {DateTime.Now:yyyy-MM-dd HH:mm:ss} — {maps.Count} maps, {dwell.TotalSeconds:0.##}s each");
            for (int i = 0; i < maps.Count && !ct.IsCancellationRequested; i++)
            {
                MapEntry m = maps[i];
                AppendSweepLine($"{m.MapId}\t{m.DisplayName}\t{DateTime.Now:HH:mm:ss}");
                Console.WriteLine($"[sweep] {i + 1}/{maps.Count} {m.MapId} {m.DisplayName}");
                await ReplyAsync(session, $"[sweep {i + 1}/{maps.Count}] {m.MapId} {m.DisplayName}").ConfigureAwait(false);
                await MovePlayerToMapAsync(session, m.MapId, spawnPortal: 0).ConfigureAwait(false);
                await Task.Delay(dwell, ct).ConfigureAwait(false);
            }

            if (!ct.IsCancellationRequested)
            {
                AppendSweepLine("# done");
                Console.WriteLine("[sweep] done — every map entered without losing the client");
                await ReplyAsync(session, "sweep: 全マップ完了。落ちたマップはありません").ConfigureAwait(false);
                _sweep = null;   // finished: a later logout is a logout, not a crash
            }
        }
        catch (OperationCanceledException)
        {
            // stopped, or the client went away
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[sweep] stopped: {ex.Message}");
        }
    }

    // ----- unhandled client packets: never silence (docs/TASK.md フェーズ0) ----------------

    /// <summary>Opcode names this session already reported, so the log line and the [DEV] line come once each.</summary>
    private readonly HashSet<string> _unhandledSeen = new(StringComparer.Ordinal);

    /// <summary>
    /// Requests the client answers with an inventory lock: until a response arrives the player can
    /// not touch the inventory. The oracle's contract for a failed request is an empty
    /// InventoryOperation (unlock), so an unimplemented one gets that too. Every *UseRequest is in
    /// this family by name; these are the rest.
    /// </summary>
    private static readonly HashSet<string> InventoryLockingRequests = new(StringComparer.Ordinal)
    {
        "CP_UserRepairDurability", "CP_UserRepairDurabilityAll", "CP_UserItemMakeRequest",
        "CP_UserUseGachaponBoxRequest", "CP_UserItemReleaseRequest", "CP_UserActivateEffectItem",
        "CP_CashGachaponOpenRequest", "CP_UserChangeStatRequestByItemOption", "CP_UserUpgradeTombEffect",
    };

    /// <summary>Packets the client sends on its own (probes, timers, mob bookkeeping): logged, never announced.</summary>
    private static readonly HashSet<string> SilentUnhandled = new(StringComparer.Ordinal)
    {
        "CP_INVITE_PARTY_MATCH", "CP_CANCEL_INVITE_PARTY_MATCH", "CP_RequestFootHoldInfo", "CP_FootHoldInfo",
        "CP_RequireFieldObstacleStatus", "CP_PetUpdateExceptionListRequest", "CP_MobRequestEscortInfo",
        "CP_MobEscortStopEndRequest", "CP_MobDropPickUpRequest", "CP_UserTemporaryStatUpdateRequest",
        "CP_UserCalcDamageStatSetRequest", "CP_CashShopQueryCashRequest", "CP_CashShopChargeParamRequest",
        "CP_UserHP", "CP_UserBanMapByMob", "CP_MobHitByObstacle", "CP_MobHitByMob", "CP_MobSelfDestruct",
        "CP_MobAttackMob", "CP_MobSkillDelayEnd", "CP_MobTimeBombEnd", "CP_MobEscortCollision",
        "CP_PassiveskillInfoUpdate", "CP_QuickslotKeyMappedModified", "CP_ExceptionLog", "CP_Log",
        "CP_SecurityPacket", "CP_UserEffectLocal",
    };

    /// <summary>Player actions whose opcode name lacks the "Request" suffix but still deserve the [DEV] line.</summary>
    private static readonly HashSet<string> UnhandledActions = new(StringComparer.Ordinal)
    {
        "CP_GuildBBS", "CP_UserThrowGrenade", "CP_TalkToTutor", "CP_UserRepairDurability",
        "CP_UserRepairDurabilityAll", "CP_UserActivateEffectItem", "CP_UserAttackUser", "CP_UserBodyAttack",
        "CP_ReactorTouch", "CP_EventStart", "CP_SnowBallHit", "CP_CoconutHit", "CP_UserMonsterBookSetCover",
        "CP_AllianceResult", "CP_FamilyJoinResult", "CP_FamilySummonResult", "CP_JMS_JUKEBOX",
        "CP_JMS_MapleGift", "CP_JMS_Poll_Answer", "CP_JMS_PachinkoPrizes", "CP_UserADBoardClose",
    };

    /// <summary>
    /// A packet none of the handlers claim. The rule since 2026-09-08: never silence. Whatever lock
    /// the client took for the request is released (empty InventoryOperation, or the
    /// transfer-ignored packet for map requests), the player sees one [DEV] line per opcode per
    /// session, and the server log gets <c>[dev] unhandled …</c> — the list of what players really
    /// tried is the work list for docs/TASK.md フェーズ0.
    /// </summary>
    private async ValueTask HandleUnhandledAsync(MapleSession session, int opcode)
    {
        string name = _clientOps.NameOf(opcode) ?? $"0x{opcode:X4}";
        bool first = _unhandledSeen.Add(name);
        if (first)
        {
            Console.WriteLine($"[dev] unhandled {name} from {_player?.Character.Name ?? "(no player)"} on map {_player?.Character.MapId ?? 0}");
        }

        if (_player is null)
        {
            return;
        }

        if (name.EndsWith("UseRequest", StringComparison.Ordinal) || InventoryLockingRequests.Contains(name))
        {
            await session.SendAsync(_packets.InventoryOperation(Array.Empty<InventoryChange>())).ConfigureAwait(false);
        }
        else if (name is "CP_UserMapTransferRequest" or "CP_UserPortalTeleportRequest" or "CP_EnterOpenGateRequest")
        {
            await session.SendAsync(_packets.TransferFieldReqIgnored(TransferDisabledPortal)).ConfigureAwait(false);
        }

        bool playerAction = !SilentUnhandled.Contains(name)
            && (name.EndsWith("Request", StringComparison.Ordinal) || UnhandledActions.Contains(name));
        if (first && playerAction)
        {
            await ReplyAsync(session, $"{NpcConversation.DevPrefix}この操作はまだ実装されていません（{name}）").ConfigureAwait(false);
        }
    }

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
            cm.sendDev("このNPCはまだ実装されていません。\r\n（今は特に話すことはないようだ……）");
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
    /// flight map (ports <c>ReqCField.OnContiState</c>): every station answers "docked"
    /// (<c>CONTI_WAIT</c>). On a flight map the oracle always answers <c>CONTI_MOBGEN</c> (the
    /// Balrog ship is alongside from the first second — it never runs a timeline); we answer the
    /// timeline's current state instead (<c>CONTI_MOVE</c> while the skies are clear, <c>MOBGEN</c>
    /// while the raid is on), and the raid itself — enemy ship, Balrogs, departure — is driven by
    /// <see cref="AirshipService"/>, never from this entry handshake.
    /// </summary>
    private const double GmMoveDefaultSpeedTimes = 3.0;
    private const double GmMoveDefaultJumpTimes = 1.8;
    private const double GmMoveDefaultAttackTimes = 1.0;

    /// <summary>Every temporary stat /gmmove may set — what OFF resets.</summary>
    private static readonly UInt128 GmMoveMask =
        (UInt128.One << BuffEffect.Speed) | (UInt128.One << BuffEffect.Jump) | (UInt128.One << BuffEffect.Booster);

    /// <summary>The reason (skill id) on /gmmove's Speed and Jump stats: Haste (4101004), a skill
    /// that grants exactly those two, so the buff icon reads right. It must NOT be the soaring
    /// skill 1026 (天の翼 / 플라잉): the client treats stats carrying a soaring reason as flight, which
    /// must never happen from /gmmove.</summary>
    public const int GmMoveReasonSkill = 4101004;

    /// <summary>The reason on /gmmove's Booster stat: Sword Booster (1101004); any booster skill's
    /// icon will do, the effect comes from the value.</summary>
    public const int GmMoveBoosterReasonSkill = 1101004;

    /// <summary>The /gmmove temporary stats for multipliers of the 100% base: Speed +(N×100−100),
    /// Jump likewise, and the Booster offset from <see cref="GmMoveBooster"/> when it is not 0. A
    /// stock client clamps them (140% / 123% / degree 2); DevTools/clientpatch_speedcap.py removes
    /// the clamps. Reasons are <see cref="GmMoveReasonSkill"/> / <see cref="GmMoveBoosterReasonSkill"/>;
    /// the day-long duration is a formality — the command clears them.</summary>
    internal static BuffStat[] GmMoveBuffs(double speedTimes, double jumpTimes, int booster)
    {
        var stats = new List<BuffStat>
        {
            new(BuffEffect.Speed, (short)Math.Clamp(Math.Round(speedTimes * 100 - 100), 0, short.MaxValue), GmMoveReasonSkill, 86_400_000),
            new(BuffEffect.Jump, (short)Math.Clamp(Math.Round(jumpTimes * 100 - 100), 0, short.MaxValue), GmMoveReasonSkill, 86_400_000),
        };
        if (booster != 0)
        {
            stats.Add(new BuffStat(BuffEffect.Booster, (short)booster, GmMoveBoosterReasonSkill, 86_400_000));
        }

        return stats.ToArray();
    }

    /// <summary>
    /// The Booster value that makes attacks <paramref name="attackTimes"/> times faster. The client
    /// scales attack frame time by (degree + 10) / 16 where degree = the weapon's attackSpeed + the
    /// Booster stat (stock client: degree clamped to ≥ 2), so a factor 1/N needs degree
    /// 16/N − 10; the result is kept at degree ≥ −8 (an 8× ceiling — degree −10 would be a zero
    /// frame time) and never slower than the weapon itself.
    /// </summary>
    public static int GmMoveBooster(double attackTimes, int weaponSpeed)
    {
        if (attackTimes <= 1)
        {
            return 0;
        }

        int degree = Math.Max(-8, (int)Math.Round(16.0 / attackTimes - 10));
        return Math.Min(0, degree - weaponSpeed);
    }

    /// <summary>The equipped weapon's attackSpeed degree from the item data (6 when none / unknown).</summary>
    private int EquippedWeaponAttackSpeed(Character c)
    {
        InventoryItem? weapon = c.EquippedItems.FirstOrDefault(i => i.Position == -11);
        return weapon is null ? 6 : _items.GetEquipStats(weapon.ItemId)?.AttackSpeed ?? 6;
    }

    /// <summary>
    /// The station departure board: a map with a wz <c>clock</c> node shows the server machine's
    /// local time, sent once on entry (ports TacosMap.addPlayer's hasClock branch — LP_Clock type 1
    /// hh:mm:ss; the client keeps it ticking). Without it the board sits at 00:00.
    /// </summary>
    private async ValueTask SendFieldClockAsync(MapleSession session, int mapId)
    {
        if (_maps.GetMap(mapId)?.HasClock != true)
        {
            return;
        }

        DateTime now = DateTime.Now;
        await session.SendAsync(_packets.Clock(now.Hour, now.Minute, now.Second)).ConfigureAwait(false);
    }

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
            {
                // Calm skies: no reply (the oracle answers only its station list and the flight
                // maps). Mid-raid joiner: the oracle's MOBGEN reply — the enemy ship is alongside.
                if (AirshipSchedule.EnemyShipAt(AirshipSchedule.Clock()) == EnemyShipState.Present)
                {
                    await session.SendAsync(_packets.ContiMove(ChannelPackets.ContiTargetMoveField, ChannelPackets.ContiMobGen)).ConfigureAwait(false);
                }

                break;
            }
        }
    }

    /// <summary>The <c>player</c> object handed to NPC / quest / portal scripts.</summary>
    private ChannelPlayer CreateScriptPlayer(MapleSession session) => new(
        _player!.Character, _characters, session, _packets,
        warp: (map, portal) => MovePlayerToMapAsync(session, map, portal),
        findPortal: (map, name) => _maps.GetMap(map)?.FindPortal(name)?.Id,
        isPartyLeader: () => ((IMassacreHost)this).IsPartyLeader,
        startSubway: TryStartSubwayMassacre,
        bonusSubway: TryEnterSubwayBonus,
        questData: id => ((IMassacreHost)this).GetQuestData(id),
        setQuestData: (id, data) => ((IMassacreHost)this).SetQuestData(id, data),
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
        mobCount: () => _field?.Mobs.Count(m => !m.IsDead) ?? 0,
        dojoPoints: DojoPoints,
        setDojoPoints: points => _ = SetDojoPointsAsync(points),
        dojoEnter: DojoEnter,
        dojoNext: () => _ = DojoNextStageAsync(),
        dojoUp: () => DojoTeleportUpAsync().AsTask().GetAwaiter().GetResult(),
        dojoExit: () => _ = DojoExitAsync(),
        dojoTutorialExit: () => DojoTutorialExitAsync().AsTask().GetAwaiter().GetResult(),
        openNpc: npcId => OpenNpcFromScript(session, npcId),
        startQuest: questId => ForceStartQuestAsync(session, questId, _conversation?.NpcId ?? 0),
        completeQuest: questId => ForceCompleteQuestAsync(session, questId, _conversation?.NpcId ?? 0));

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

        if (conversation.Advance(messageType, action, selection, text) == NpcAnswerResult.Rejected)
        {
            // The answer named an option the menu never offered — only a hand-crafted packet does
            // that (Riremito: taxi menus whose ids are map ids would otherwise warp anywhere). The
            // engine has already ended the dialog; free the slot so the next NPC click works.
            _conversation = null;
            Console.WriteLine($"[npc] {_player?.Character.Name}: answered npc {conversation.NpcId} with unoffered option {selection} — dialog closed");
        }
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
        await SendFieldClockAsync(session, targetMapId).ConfigureAwait(false);
        await OnFieldEnteredAsync(targetMapId).ConfigureAwait(false);

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
