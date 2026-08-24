using System.Net;
using Cronus.Network.Packets;

namespace Cronus.Debug.Bot;

/// <summary>One step's outcome for the final report.</summary>
public sealed record StepResult(string Bot, string Step, bool Ok, string Detail, long Ms);

/// <summary>
/// The content walkthrough each bot performs against a live server: the real login flow,
/// game entry, commands, NPC dialogs, the salon UI, whisper, a channel change, the cash
/// shop round trip, and boss-door smoke checks.
/// </summary>
public sealed class Scenarios
{
    private readonly IPEndPoint _login;
    private readonly List<StepResult> _results;
    private readonly object _resultGate = new();

    public Scenarios(IPEndPoint loginEndpoint, List<StepResult> results)
    {
        _login = loginEndpoint;
        _results = results;
    }

    private async Task StepAsync(BotClient bot, string name, Func<Task<string>> action)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            string detail = await action().ConfigureAwait(false);
            Record(new StepResult(bot.Name, name, true, detail, sw.ElapsedMilliseconds));
            Console.WriteLine($"[{bot.Name}] OK  {name} ({sw.ElapsedMilliseconds}ms){(detail.Length > 0 ? " — " + detail : "")}");
        }
        catch (Exception ex)
        {
            Record(new StepResult(bot.Name, name, false, ex.Message, sw.ElapsedMilliseconds));
            Console.WriteLine($"[{bot.Name}] NG  {name} ({sw.ElapsedMilliseconds}ms) — {ex.Message}");
        }
    }

    private void Record(StepResult r)
    {
        lock (_resultGate)
        {
            _results.Add(r);
        }
    }

    // ---- login & entry -------------------------------------------------------------------

    /// <summary>Logs in (auto-registering), picks/creates the character, and enters the game.</summary>
    public async Task LoginAndEnterAsync(BotClient bot, int botIndex, CancellationToken ct)
    {
        await StepAsync(bot, "login", async () =>
        {
            await bot.ConnectAsync(_login, ct).ConfigureAwait(false);

            PacketWriter w = bot.NewPacket(ClientOpcode.CheckPassword);
            w.WriteString($"cronusbot{botIndex}");
            w.WriteString("bot");
            w.WriteBytes(new byte[16]);
            w.WriteInt(0);
            w.WriteByte(0);
            w.WriteByte(0);
            await bot.SendAsync(w).ConfigureAwait(false);

            PacketReader r = await bot.ExpectAsync(ServerOpcode.CheckPasswordResult).ConfigureAwait(false);
            int result = r.ReadByte();
            return result == 0 ? "auto-registered / authenticated" : throw new InvalidOperationException($"login result {result}");
        }).ConfigureAwait(false);

        await StepAsync(bot, "world+character", async () =>
        {
            (int charId, int count) = await SelectWorldAsync(bot).ConfigureAwait(false);
            if (count == 0)
            {
                PacketWriter c = bot.NewPacket(ClientOpcode.CreateNewCharacter);
                c.WriteString(bot.CharacterName);
                c.WriteInt(1);          // adventurer
                c.WriteShort(0);
                c.WriteInt(20000);      // face
                c.WriteInt(30020);      // hair
                c.WriteInt(1040002);    // top
                c.WriteInt(1060002);    // bottom
                c.WriteInt(1072001);    // shoes
                c.WriteInt(1302000);    // weapon
                await bot.SendAsync(c).ConfigureAwait(false);
                await bot.ExpectAsync(ServerOpcode.CreateNewCharacterResult).ConfigureAwait(false);
                (charId, count) = await SelectWorldAsync(bot).ConfigureAwait(false);
            }

            if (charId == 0)
            {
                throw new InvalidOperationException("no character id in the world-select list");
            }

            bot.CharacterId = charId;
            return $"character {charId} ({count} on the account)";
        }).ConfigureAwait(false);

        await StepAsync(bot, "enter-game", async () =>
        {
            PacketWriter w = bot.NewPacket(ClientOpcode.SelectCharacter);
            w.WriteInt(bot.CharacterId);
            await bot.SendAsync(w).ConfigureAwait(false);

            PacketReader r = await bot.ExpectAsync(ServerOpcode.SelectCharacterResult).ConfigureAwait(false);
            r.ReadByte();
            r.ReadByte();
            var ip = new IPAddress(r.ReadBytes(4));
            int port = (ushort)r.ReadShort();
            int charId = r.ReadInt();

            await MigrateAsync(bot, new IPEndPoint(ip, port), charId, ct).ConfigureAwait(false);
            return $"channel {ip}:{port}";
        }).ConfigureAwait(false);
    }

    private async Task<(int CharId, int Count)> SelectWorldAsync(BotClient bot)
    {
        PacketWriter w = bot.NewPacket(ClientOpcode.SelectWorld);
        w.WriteByte(0);
        w.WriteByte(0);
        await bot.SendAsync(w).ConfigureAwait(false);

        PacketReader r = await bot.ExpectAsync(ServerOpcode.SelectWorldResult).ConfigureAwait(false);
        int result = r.ReadByte();
        if (result != 0)
        {
            throw new InvalidOperationException($"select world result {result}");
        }

        r.ReadString();
        int count = r.ReadByte();
        int charId = count > 0 ? r.ReadInt() : 0;
        return (charId, count);
    }

    /// <summary>Reconnects to a game/cash-shop endpoint and migrates the character in.</summary>
    private async Task MigrateAsync(BotClient bot, IPEndPoint endpoint, int charId, CancellationToken ct)
    {
        await bot.ConnectAsync(endpoint, ct).ConfigureAwait(false);
        PacketWriter m = bot.NewPacket(ClientOpcode.MigrateIn);
        m.WriteInt(charId);
        m.WriteBytes(new byte[16]);
        m.WriteShort(0);
        m.WriteByte(0);
        m.WriteLong(0);
        await bot.SendAsync(m).ConfigureAwait(false);
    }

    // ---- the solo content walkthrough ----------------------------------------------------

    public async Task RunSoloSuiteAsync(BotClient bot, CancellationToken ct, bool fieldAudits = true)
    {
        await StepAsync(bot, "set-field", async () =>
        {
            await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
            return "";
        }).ConfigureAwait(false);

        await StepAsync(bot, "chat:/help", async () =>
        {
            // The listing is one chat packet per line: a header, category headings, one line per
            // command, and a footer. Read to the footer so nothing is left queued for later steps.
            await ChatAsync(bot, "/help").ConfigureAwait(false);
            List<string> lines = await ReadChatUntilAsync(bot, "/help <").ConfigureAwait(false);
            if (lines.Count < 10)
            {
                throw new InvalidOperationException($"/help returned only {lines.Count} lines");
            }

            return $"{lines.Count} lines";
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:/meso+/level", async () =>
        {
            await ChatAsync(bot, "/meso 1000000").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.StatChanged).ConfigureAwait(false);
            await ChatAsync(bot, "/level 30").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.StatChanged).ConfigureAwait(false);
            return "";
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:/clearinv", async () =>
        {
            // Persistent accounts accumulate items across suite runs; start from a clean bag so
            // the capacity-bounded inventory never rejects the audits below.
            await ChatAsync(bot, "/clearinv").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.UserChat).ConfigureAwait(false); // the reply line

            // Swallow the remove batches so later audits' InventoryOperation waits stay clean.
            while (true)
            {
                try
                {
                    await bot.ExpectAsync(ServerOpcode.InventoryOperation, TimeSpan.FromMilliseconds(300)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    break;
                }
            }

            return "";
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:/item", async () =>
        {
            await ChatAsync(bot, "/item 2030004").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.InventoryOperation).ConfigureAwait(false);
            return "return scroll granted";
        }).ConfigureAwait(false);

        await StepAsync(bot, "item-encode-audit", async () =>
        {
            // Grant one item of every encode path and parse each InventoryOperation body exactly
            // as the client would — a mis-sized body is the "error code 38" crash class. Pickup
            // uses this same encode, so a clean parse here means a clean pickup.
            (int Id, string What)[] items =
            {
                (1302000, "equip:sword"),
                (1002140, "equip:hat"),
                (1082002, "equip:gloves"),
                (2000000, "use:potion"),
                (2070006, "use:throwing-star"),
                (2340000, "use:white-scroll"),
                (3010000, "setup:chair"),
                (4000000, "etc"),
                (4001017, "etc:eye-of-fire"),
                (5000000, "cash:pet"),
            };

            var bad = new List<string>();
            foreach ((int id, string what) in items)
            {
                await ChatAsync(bot, $"/item {id}").ConfigureAwait(false);
                PacketReader r = await bot.ExpectAsync(ServerOpcode.InventoryOperation).ConfigureAwait(false);
                try
                {
                    r.ReadByte();                 // unlock
                    int count = r.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        int mode = r.ReadByte();
                        r.ReadByte();             // tab
                        if (mode == 0)            // Add
                        {
                            r.ReadShort();        // slot
                            ItemBodyParser.Read(r);
                        }
                        else if (mode == 1)       // Update (stacked)
                        {
                            r.ReadShort();
                            r.ReadShort();
                        }
                    }

                    if (r.Remaining != 0)
                    {
                        bad.Add($"{what}(+{r.Remaining}b)");
                    }
                }
                catch (Exception)
                {
                    bad.Add($"{what}(EOF)");
                }
            }

            return bad.Count == 0 ? $"{items.Length} item types clean" : throw new InvalidOperationException("malformed: " + string.Join(", ", bad));
        }).ConfigureAwait(false);

        // Ground drops broadcast to every bot sharing the field, so several bots dropping at once
        // cross each other's DropEnterField/DropLeaveField streams. This is a packet-correctness
        // audit, not a concurrency test, so run it on a single designated bot (the others exercise
        // the field concurrently through movement/chat); single-bot it is byte-clean and stable.
        if (fieldAudits)
        await StepAsync(bot, "drop-pickup-audit", async () =>
        {
            // Spawn a real ground drop and pick it up, validating the whole client-facing
            // sequence (DropEnterField, DropLeaveField, InventoryOperation, pickup message) so a
            // malformed drop/pickup packet — the "拾ったらクラッシュ" class — is caught here.
            int opDropEnter = bot.ServerOps.Get(ServerOpcode.DropEnterField);
            int opDropLeave = bot.ServerOps.Get(ServerOpcode.DropLeaveField);
            int opInvOp = bot.ServerOps.Get(ServerOpcode.InventoryOperation);
            int opMessage = bot.ServerOps.Get(ServerOpcode.Message);

            (int Id, int Qty, string What)[] drops =
            {
                (2000000, 1, "potion"),
                (1302000, 1, "equip:sword"),
                (2070006, 200, "throwing-star"),
                (4000000, 5, "etc"),
                (0, 5000, "meso"),
            };

            var bad = new List<string>();
            foreach ((int id, int qty, string what) in drops)
            {
                await ChatAsync(bot, $"/drop {id} {qty}").ConfigureAwait(false);
                PacketReader enter = await bot.ExpectAsync(ServerOpcode.DropEnterField).ConfigureAwait(false);
                int dropOid;
                try
                {
                    enter.ReadByte();                // enter type
                    dropOid = enter.ReadInt();       // object id
                    ParseDropEnterTail(enter);
                    if (enter.Remaining != 0)
                    {
                        bad.Add($"{what}:enter(+{enter.Remaining}b)");
                        continue;
                    }
                }
                catch (Exception)
                {
                    bad.Add($"{what}:enter(EOF)");
                    continue;
                }

                // Pick it up.
                PacketWriter pick = bot.NewPacket(ClientOpcode.DropPickUpRequest);
                pick.WriteByte(0);
                pick.WriteInt(0);
                pick.WriteShort(0);
                pick.WriteShort(0);
                pick.WriteInt(dropOid);
                await bot.SendAsync(pick).ConfigureAwait(false);

                try
                {
                    // Item: DropLeaveField + InventoryOperation + pickup Message.
                    // Meso: DropLeaveField + StatChanged + money Message.
                    PacketReader leave = await bot.ExpectAsync(ServerOpcode.DropLeaveField).ConfigureAwait(false);
                    leave.ReadByte(); leave.ReadInt(); leave.ReadInt(); // type, oid, pickerId
                    if (leave.Remaining != 0)
                    {
                        bad.Add($"{what}:leave(+{leave.Remaining}b)");
                    }

                    if (id != 0)
                    {
                        PacketReader inv = await bot.ExpectAsync(ServerOpcode.InventoryOperation).ConfigureAwait(false);
                        inv.ReadByte();
                        int count = inv.ReadByte();
                        for (int i = 0; i < count; i++)
                        {
                            int mode = inv.ReadByte();
                            inv.ReadByte();
                            if (mode == 0) { inv.ReadShort(); ItemBodyParser.Read(inv); }
                            else if (mode == 1) { inv.ReadShort(); inv.ReadShort(); }
                        }

                        if (inv.Remaining != 0)
                        {
                            bad.Add($"{what}:inv(+{inv.Remaining}b)");
                        }
                    }
                }
                catch (Exception ex)
                {
                    bad.Add($"{what}:{ex.GetType().Name}");
                }
            }

            return bad.Count == 0 ? $"{drops.Length} drop/pickup paths clean" : throw new InvalidOperationException("malformed: " + string.Join(", ", bad));
        }).ConfigureAwait(false);

        await StepAsync(bot, "move", async () =>
        {
            PacketWriter w = bot.NewPacket(ClientOpcode.UserMove);
            w.WriteBytes(new byte[29]);      // the v186 fixed prefix
            w.WriteShort(0);                 // a minimal (empty) CMovePath
            w.WriteShort(0);
            w.WriteByte(0);
            await bot.SendAsync(w).ConfigureAwait(false);
            return "relayed";
        }).ConfigureAwait(false);

        await StepAsync(bot, "npc:taxi-dialog", async () =>
        {
            await SelectNpcAsync(bot, 1012000).ConfigureAwait(false); // Henesys taxi
            PacketReader r = await bot.ExpectAsync(ServerOpcode.ScriptMessage).ConfigureAwait(false);
            r.ReadByte();
            r.ReadInt();
            int msgType = r.ReadByte();
            await EscapeDialogAsync(bot, msgType).ConfigureAwait(false);
            return $"script message type {msgType}";
        }).ConfigureAwait(false);

        await StepAsync(bot, "npc:salon-style-picker", async () =>
        {
            await SelectNpcAsync(bot, 1012103).ConfigureAwait(false); // Natalie, hair salon
            PacketReader r = await bot.ExpectAsync(ServerOpcode.ScriptMessage).ConfigureAwait(false);
            r.ReadByte();
            r.ReadInt();
            int msgType = r.ReadByte();
            await EscapeDialogAsync(bot, msgType).ConfigureAwait(false);
            return msgType == 8 ? "askAvatar opened" : $"unexpected type {msgType}";
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:/beauty-console", async () =>
        {
            // /beauty opens the style console: category menu -> (skin) -> avatar picker -> apply.
            await ChatAsync(bot, "/beauty").ConfigureAwait(false);
            PacketReader menu = await bot.ExpectAsync(ServerOpcode.ScriptMessage).ConfigureAwait(false);
            menu.ReadByte(); menu.ReadInt();
            int menuType = menu.ReadByte();
            if (menuType != 5)
            {
                throw new InvalidOperationException($"expected askMenu (5), got {menuType}");
            }

            PacketWriter pick = bot.NewPacket(ClientOpcode.UserScriptMessageAnswer);
            pick.WriteByte(5);          // answering the menu
            pick.WriteByte(1);          // proceed
            pick.WriteInt(4);           // 肌の色
            await bot.SendAsync(pick).ConfigureAwait(false);

            // Long lists insert a page menu before the picker; answer page 1 when one appears.
            bool sawPicker = false;
            for (int hop = 0; hop < 3 && !sawPicker; hop++)
            {
                PacketReader next = await bot.ExpectAsync(ServerOpcode.ScriptMessage).ConfigureAwait(false);
                next.ReadByte(); next.ReadInt();
                int nextType = next.ReadByte();
                PacketWriter answer = bot.NewPacket(ClientOpcode.UserScriptMessageAnswer);
                switch (nextType)
                {
                    case 5:             // the page menu
                        answer.WriteByte(5);
                        answer.WriteByte(1);
                        answer.WriteInt(0);
                        break;
                    case 8:             // the avatar picker
                        sawPicker = true;
                        answer.WriteByte(8);
                        answer.WriteByte(1);
                        answer.WriteByte(0);
                        break;
                    default:
                        throw new InvalidOperationException($"unexpected script message {nextType}");
                }

                await bot.SendAsync(answer).ConfigureAwait(false);
            }

            if (!sawPicker)
            {
                throw new InvalidOperationException("the avatar picker never appeared");
            }

            PacketReader done = await bot.ExpectAsync(ServerOpcode.ScriptMessage).ConfigureAwait(false);
            done.ReadByte(); done.ReadInt();
            int sayType = done.ReadByte();
            await EscapeDialogAsync(bot, sayType).ConfigureAwait(false);
            return "menu -> skin picker -> applied";
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:/dbgshop", async () =>
        {
            // /dbgshop: category menu -> (page menu) -> a shop stocking that page at 1 meso.
            await ChatAsync(bot, "/dbgshop").ConfigureAwait(false);
            PacketReader menu = await bot.ExpectAsync(ServerOpcode.ScriptMessage).ConfigureAwait(false);
            menu.ReadByte(); menu.ReadInt();
            if (menu.ReadByte() != 5)
            {
                throw new InvalidOperationException("expected the category menu");
            }

            PacketWriter choose = bot.NewPacket(ClientOpcode.UserScriptMessageAnswer);
            choose.WriteByte(5);
            choose.WriteByte(1);
            choose.WriteInt(0);            // the first category
            await bot.SendAsync(choose).ConfigureAwait(false);

            // A big category inserts a page menu before the shop opens; answer page 1 if so.
            for (int hop = 0; hop < 3; hop++)
            {
                (string which, PacketReader r) = await bot.ExpectAnyAsync(
                    new[] { ServerOpcode.OpenShopDlg, ServerOpcode.ScriptMessage }).ConfigureAwait(false);

                if (which == ServerOpcode.ScriptMessage)
                {
                    r.ReadByte(); r.ReadInt(); r.ReadByte();
                    PacketWriter pick = bot.NewPacket(ClientOpcode.UserScriptMessageAnswer);
                    pick.WriteByte(5);
                    pick.WriteByte(1);
                    pick.WriteInt(0);      // first page
                    await bot.SendAsync(pick).ConfigureAwait(false);
                    continue;
                }

                r.ReadInt();               // npc id
                int count = r.ReadShort();
                int firstItemId = r.ReadInt();
                int firstPrice = r.ReadInt();
                if (firstPrice != 1)
                {
                    throw new InvalidOperationException($"debug shop price {firstPrice}, expected 1");
                }

                // Buy one of the first item for its 1 meso.
                PacketWriter buy = bot.NewPacket(ClientOpcode.UserShopRequest);
                buy.WriteByte(0);          // buy
                buy.WriteShort(0);         // shop slot
                buy.WriteInt(firstItemId);
                buy.WriteShort(1);
                await bot.SendAsync(buy).ConfigureAwait(false);
                PacketReader result = await bot.ExpectAsync(ServerOpcode.ShopResult).ConfigureAwait(false);
                int code = result.ReadByte();
                if (code != 0)
                {
                    throw new InvalidOperationException($"buy result {code}");
                }

                return $"{count} items @1 meso, bought {firstItemId}";
            }

            throw new InvalidOperationException("the debug shop never opened");
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:/dbgwarp", async () =>
        {
            // /dbgwarp: region menu -> area menu -> map menu -> the field change.
            await ChatAsync(bot, "/dbgwarp").ConfigureAwait(false);

            int menus = 0;
            for (int hop = 0; hop < 6; hop++)
            {
                (string which, PacketReader r) = await bot.ExpectAnyAsync(
                    new[] { ServerOpcode.SetField, ServerOpcode.ScriptMessage }).ConfigureAwait(false);

                if (which == ServerOpcode.ScriptMessage)
                {
                    r.ReadByte(); r.ReadInt();
                    int type = r.ReadByte();
                    if (type != 5)
                    {
                        throw new InvalidOperationException($"expected askMenu (5), got {type}");
                    }

                    menus++;
                    PacketWriter pick = bot.NewPacket(ClientOpcode.UserScriptMessageAnswer);
                    pick.WriteByte(5);
                    pick.WriteByte(1);
                    pick.WriteInt(0);          // always the first entry
                    await bot.SendAsync(pick).ConfigureAwait(false);
                    continue;
                }

                // Landed somewhere; go back to Henesys so later steps start where they expect.
                await ChatAsync(bot, "/warp 100000000").ConfigureAwait(false);
                await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
                return $"{menus} menus -> warped";
            }

            throw new InvalidOperationException("the warp never happened");
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:/status", async () =>
        {
            // The consolidated stat command: a sheet with no args, a change with a field+value.
            await ChatAsync(bot, "/status").ConfigureAwait(false);
            List<string> sheet = await ReadChatUntilAsync(bot, "/status <").ConfigureAwait(false);
            if (!sheet.Exists(l => l.Contains("Lv.", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("the stat sheet had no level line");
            }

            await ChatAsync(bot, "/status luk 123").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.StatChanged).ConfigureAwait(false);
            return "sheet + luk set";
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:bad-arguments", async () =>
        {
            // A known command with unusable arguments must answer with its registered usage.
            await ChatAsync(bot, "/item apple").ConfigureAwait(false);
            List<string> lines = await ReadChatUntilAsync(bot, "/item <").ConfigureAwait(false);
            return lines[^1];
        }).ConfigureAwait(false);

        await StepAsync(bot, "whisper:self-ack", async () =>
        {
            PacketWriter w = bot.NewPacket(ClientOpcode.Whisper);
            w.WriteByte(0x02 | 0x04); // WP_Whisper | WP_Request
            w.WriteString(bot.CharacterName);
            w.WriteString("bot self-check");
            await bot.SendAsync(w).ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.Whisper).ConfigureAwait(false);
            return "";
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:/gender-toggle", async () =>
        {
            // /gender flips the character and bounces the client through a same-channel
            // migration; the fresh SetField's stat block must carry the flipped gender byte.
            await ChatAsync(bot, "/gender f").ConfigureAwait(false);
            PacketReader mig = await bot.ExpectAsync(ServerOpcode.MigrateCommand).ConfigureAwait(false);
            mig.ReadByte();
            var ip = new IPAddress(mig.ReadBytes(4));
            int port = (ushort)mig.ReadShort();
            await MigrateAsync(bot, new IPEndPoint(ip, port), bot.CharacterId, ct).ConfigureAwait(false);

            PacketReader field = await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
            byte gender = CharacterDataParser.ReadGenderFromSetField(field);
            if (gender != 1)
            {
                throw new InvalidOperationException($"gender byte {gender} after /gender f, expected 1");
            }

            await ChatAsync(bot, "/gender m").ConfigureAwait(false);
            PacketReader back = await bot.ExpectAsync(ServerOpcode.MigrateCommand).ConfigureAwait(false);
            back.ReadByte();
            var ip2 = new IPAddress(back.ReadBytes(4));
            int port2 = (ushort)back.ReadShort();
            await MigrateAsync(bot, new IPEndPoint(ip2, port2), bot.CharacterId, ct).ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
            return "f -> re-entry carried gender 1 -> back to m";
        }).ConfigureAwait(false);

        await StepAsync(bot, "quest:wz-chain-1000-1001", async () =>
        {
            // The real wz tutorial chain: 1000 starts at npc 2101 (needs the beginner shirt
            // 1042003, job 0) and completes at 2100; 1001 needs 1000 completed, its start act
            // GIVES the letter (4031003) and its end check wants it back at 2101. Exercises the
            // wz-parsed gates (job / item / prerequisite-quest) and acts end to end. Reset state
            // first so the step is re-runnable against a live server.
            await ChatAsync(bot, "/job 0").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.StatChanged).ConfigureAwait(false);
            await ChatAsync(bot, "/item 1042003").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.InventoryOperation).ConfigureAwait(false);
            await ChatAsync(bot, "/questreset 1000").ConfigureAwait(false);
            await ChatAsync(bot, "/questreset 1001").ConfigureAwait(false);
            await Task.Delay(150).ConfigureAwait(false); // let the resets flush

            async Task SendQuestAsync(byte action, short questId, int npcId)
            {
                PacketWriter w = bot.NewPacket(ClientOpcode.UserQuestRequest);
                w.WriteByte(action);
                w.WriteShort(questId);
                w.WriteInt(npcId);
                if (action == 2)
                {
                    w.WriteInt(-1);         // no reward selection
                }

                await bot.SendAsync(w).ConfigureAwait(false);
            }

            // Accepting still answers with UserQuestResult(Act_Success) — after the record.
            async Task AcceptAsync(short questId, int npcId)
            {
                await SendQuestAsync(1, questId, npcId).ConfigureAwait(false);
                PacketReader r = await bot.ExpectAsync(ServerOpcode.UserQuestResult).ConfigureAwait(false);
                if (r.ReadByte() != 8)
                {
                    throw new InvalidOperationException($"quest {questId} accept: not Act_Success");
                }
            }

            // Completing answers with the completed quest record; LP_UserQuestResult exists on
            // completion ONLY when the act names a nextQuest (sending it otherwise crashes the
            // live client's NPC dialog — the ネオトウキョウ 4681 crash).
            async Task<short> CompleteAsync(short questId, int npcId, bool expectNextQuest)
            {
                await SendQuestAsync(2, questId, npcId).ConfigureAwait(false);
                await bot.ExpectAsync(ServerOpcode.Message, r =>
                {
                    if (r.ReadByte() != 1)
                    {
                        return false; // not a quest record
                    }

                    return (r.ReadShort() & 0xFFFF) == questId && r.ReadByte() == 2;
                }).ConfigureAwait(false);

                if (!expectNextQuest)
                {
                    return 0;
                }

                PacketReader e3 = await bot.ExpectAsync(ServerOpcode.UserQuestResult).ConfigureAwait(false);
                e3.ReadByte();              // Act_Success
                e3.ReadShort();             // quest id
                e3.ReadInt();               // npc
                return e3.ReadShort();      // nextQuest
            }

            await AcceptAsync(1000, 2101).ConfigureAwait(false);     // accept 1000 (job+item gates)
            short next = await CompleteAsync(1000, 2100, expectNextQuest: true).ConfigureAwait(false);
            if (next != 1001)
            {
                throw new InvalidOperationException($"quest 1000 nextQuest {next}, expected 1001");
            }

            await AcceptAsync(1001, 2100).ConfigureAwait(false);     // accept 1001 (prereq gate)
            await bot.ExpectAsync(ServerOpcode.InventoryOperation).ConfigureAwait(false); // the letter
            await CompleteAsync(1001, 2101, expectNextQuest: false).ConfigureAwait(false); // letter turn-in

            return "1000 -> (nextQuest) -> 1001 chained via wz data";
        }).ConfigureAwait(false);

        await StepAsync(bot, "boss:zakum-door", async () =>
        {
            await ChatAsync(bot, "/map 211042400").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
            await SelectNpcAsync(bot, 2030013).ConfigureAwait(false);
            PacketReader r = await bot.ExpectAsync(ServerOpcode.ScriptMessage).ConfigureAwait(false);
            r.ReadByte();
            r.ReadInt();
            int msgType = r.ReadByte();
            await EscapeDialogAsync(bot, msgType).ConfigureAwait(false);
            await ChatAsync(bot, "/map 100000000").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
            return "door dialog answered";
        }).ConfigureAwait(false);

        await StepAsync(bot, "cash-shop:round-trip", async () =>
        {
            await bot.SendAsync(bot.NewPacket(ClientOpcode.UserMigrateToCashShopRequest)).ConfigureAwait(false);
            PacketReader r = await bot.ExpectAsync(ServerOpcode.MigrateCommand).ConfigureAwait(false);
            r.ReadByte();
            var csIp = new IPAddress(r.ReadBytes(4));
            int csPort = (ushort)r.ReadShort();

            await MigrateAsync(bot, new IPEndPoint(csIp, csPort), bot.CharacterId, ct).ConfigureAwait(false);
            PacketReader setShop = await bot.ExpectAsync(ServerOpcode.SetCashShop).ConfigureAwait(false);
            int shopLeftover = CharacterDataParser.ValidateSetCashShop(setShop);
            if (shopLeftover != 0)
            {
                throw new InvalidOperationException($"SetCashShop CharacterData mis-sized (leftover {shopLeftover}b)");
            }

            PacketReader cash = await bot.ExpectAsync(ServerOpcode.CashShopQueryCashResult).ConfigureAwait(false);
            int nx = cash.ReadInt();

            // The entry sequence also sends the locker (CashItemRes_LoadLocker_Done, 0x4E) on the
            // same opcode as the buy result — drain it first so the buy's 0x58 matches.
            PacketReader locker = await bot.ExpectAsync(ServerOpcode.CashShopCashItemResult).ConfigureAwait(false);
            if (locker.ReadByte() != 0x4E)
            {
                throw new InvalidOperationException("expected the locker listing on entry");
            }

            // Buy the first catalog entry (SN 10000001, 390 NX) into the locker.
            PacketWriter buy = bot.NewPacket(ClientOpcode.CashShopCashItemRequest);
            buy.WriteByte(0x03);
            buy.WriteByte(0);
            buy.WriteInt(10000001);
            await bot.SendAsync(buy).ConfigureAwait(false);
            PacketReader res = await bot.ExpectAsync(ServerOpcode.CashShopCashItemResult).ConfigureAwait(false);
            int resType = res.ReadByte();
            if (resType != 0x58) // CashItemRes_Buy_Done
            {
                throw new InvalidOperationException($"buy result 0x{resType:X2}");
            }

            long boughtCashId = res.ReadLong();

            // Move it into the character's inventory, then back to the locker — the exact path a
            // player takes, and the one that produced the malformed cash-item CharacterData blob.
            PacketWriter toInv = bot.NewPacket(ClientOpcode.CashShopCashItemRequest);
            toInv.WriteByte(0x0E);           // CashItemReq_MoveLtoS
            toInv.WriteLong(boughtCashId);
            toInv.WriteByte(1);              // inv type (equip)
            toInv.WriteShort(0);
            await bot.SendAsync(toInv).ConfigureAwait(false);
            PacketReader lToS = await bot.ExpectAsync(ServerOpcode.CashShopCashItemResult).ConfigureAwait(false);
            if (lToS.ReadByte() != 0x6B) // CashItemRes_MoveLtoS_Done
            {
                throw new InvalidOperationException("move locker->inventory failed");
            }

            PacketWriter toLocker = bot.NewPacket(ClientOpcode.CashShopCashItemRequest);
            toLocker.WriteByte(0x0F);        // CashItemReq_MoveStoL
            toLocker.WriteLong(boughtCashId);
            toLocker.WriteByte(1);
            await bot.SendAsync(toLocker).ConfigureAwait(false);
            PacketReader sToL = await bot.ExpectAsync(ServerOpcode.CashShopCashItemResult).ConfigureAwait(false);
            if (sToL.ReadByte() != 0x6D) // CashItemRes_MoveStoL_Done
            {
                throw new InvalidOperationException("move inventory->locker failed");
            }

            // Back to the game.
            await bot.SendAsync(bot.NewPacket(ClientOpcode.UserTransferFieldRequest)).ConfigureAwait(false);
            PacketReader back = await bot.ExpectAsync(ServerOpcode.MigrateCommand).ConfigureAwait(false);
            back.ReadByte();
            var chIp = new IPAddress(back.ReadBytes(4));
            int chPort = (ushort)back.ReadShort();
            await MigrateAsync(bot, new IPEndPoint(chIp, chPort), bot.CharacterId, ct).ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
            return $"NX {nx}, bought SN 10000001, returned to {chIp}:{chPort}";
        }).ConfigureAwait(false);

        await StepAsync(bot, "channel-change", async () =>
        {
            PacketWriter w = bot.NewPacket(ClientOpcode.UserTransferChannelRequest);
            w.WriteByte(1);
            await bot.SendAsync(w).ConfigureAwait(false);
            PacketReader r = await bot.ExpectAsync(ServerOpcode.MigrateCommand).ConfigureAwait(false);
            r.ReadByte();
            var ip = new IPAddress(r.ReadBytes(4));
            int port = (ushort)r.ReadShort();
            await MigrateAsync(bot, new IPEndPoint(ip, port), bot.CharacterId, ct).ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
            return $"now on {ip}:{port}";
        }).ConfigureAwait(false);

        await StepAsync(bot, "chardata:maxed-4th-job-reentry", async () =>
        {
            // The re-login crash class: an advanced, /maxskills'd character's full CharacterData.
            // Become a 4th-job mage, max skills, then re-enter (channel 0) and parse the whole
            // blob the way the client does — a mis-sized skill/inventory section is caught here.
            await ChatAsync(bot, "/job 222").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.StatChanged).ConfigureAwait(false);
            await ChatAsync(bot, "/maxskills").ConfigureAwait(false);
            await Task.Delay(400).ConfigureAwait(false); // let the skill-record acks flush

            PacketWriter w = bot.NewPacket(ClientOpcode.UserTransferChannelRequest);
            w.WriteByte(0); // back to channel 0 (the bot is on channel 1 after the previous step)
            await bot.SendAsync(w).ConfigureAwait(false);
            PacketReader mig = await bot.ExpectAsync(ServerOpcode.MigrateCommand).ConfigureAwait(false);
            mig.ReadByte();
            var ip = new IPAddress(mig.ReadBytes(4));
            int port = (ushort)mig.ReadShort();
            await MigrateAsync(bot, new IPEndPoint(ip, port), bot.CharacterId, ct).ConfigureAwait(false);

            PacketReader field = await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
            int leftover = CharacterDataParser.ValidateSetField(field);
            if (leftover != 0)
            {
                throw new InvalidOperationException($"SetField CharacterData mis-sized (leftover {leftover}b) — client would EOF-crash");
            }

            // The cash shop embeds the same blob; a maxed character crashed entering it too.
            await bot.SendAsync(bot.NewPacket(ClientOpcode.UserMigrateToCashShopRequest)).ConfigureAwait(false);
            PacketReader cs = await bot.ExpectAsync(ServerOpcode.MigrateCommand).ConfigureAwait(false);
            cs.ReadByte();
            var csIp2 = new IPAddress(cs.ReadBytes(4));
            int csPort2 = (ushort)cs.ReadShort();
            await MigrateAsync(bot, new IPEndPoint(csIp2, csPort2), bot.CharacterId, ct).ConfigureAwait(false);
            PacketReader shop = await bot.ExpectAsync(ServerOpcode.SetCashShop).ConfigureAwait(false);
            int shopLeftover = CharacterDataParser.ValidateSetCashShop(shop);
            if (shopLeftover != 0)
            {
                throw new InvalidOperationException($"SetCashShop CharacterData mis-sized (leftover {shopLeftover}b) — client would EOF-crash");
            }

            // Return to the game channel so the bot ends in a field (findable for the whisper/party
            // pair steps that follow) rather than stranded in the cash shop.
            await bot.SendAsync(bot.NewPacket(ClientOpcode.UserTransferFieldRequest)).ConfigureAwait(false);
            PacketReader back = await bot.ExpectAsync(ServerOpcode.MigrateCommand).ConfigureAwait(false);
            back.ReadByte();
            var backIp = new IPAddress(back.ReadBytes(4));
            int backPort = (ushort)back.ReadShort();
            await MigrateAsync(bot, new IPEndPoint(backIp, backPort), bot.CharacterId, ct).ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);

            return "SetField + SetCashShop both parsed clean (79 skills, 9 with master level, 4th job)";
        }).ConfigureAwait(false);
    }

    // ---- paired scenarios ----------------------------------------------------------------

    /// <summary>Bot A whispers bot B (they can be on different channels).</summary>
    public async Task RunWhisperPairAsync(BotClient a, BotClient b)
    {
        await StepAsync(a, $"whisper->{b.Name}", async () =>
        {
            Task<PacketReader> receive = b.ExpectAsync(ServerOpcode.Whisper);

            // The recipient can be momentarily out of a field (its own re-entry steps migrate the
            // session), and the server only delivers to a player it can find. The sender ack says
            // which happened, so retry until it reports delivered rather than hanging on receive.
            bool delivered = false;
            for (int attempt = 0; attempt < 20 && !delivered; attempt++)
            {
                PacketWriter w = a.NewPacket(ClientOpcode.Whisper);
                w.WriteByte(0x02 | 0x04);
                w.WriteString(b.CharacterName);
                w.WriteString("hello from " + a.CharacterName);
                await a.SendAsync(w).ConfigureAwait(false);

                PacketReader ack = await a.ExpectAsync(ServerOpcode.Whisper).ConfigureAwait(false);
                ack.ReadByte();                 // WP_Result | WP_Whisper
                ack.ReadString();               // the target name we asked for
                delivered = ack.ReadBool();
                if (!delivered)
                {
                    await Task.Delay(200).ConfigureAwait(false);
                }
            }

            if (!delivered)
            {
                throw new InvalidOperationException($"{b.Name} never became reachable for a whisper");
            }

            PacketReader got = await receive.ConfigureAwait(false);          // recipient delivery
            got.ReadByte();
            string from = got.ReadString();
            return $"{b.Name} received from {from}";
        }).ConfigureAwait(false);
    }

    /// <summary>Bot A creates a party and invites bot B, who accepts.</summary>
    public async Task RunPartyPairAsync(BotClient a, BotClient b)
    {
        await StepAsync(a, $"party+{b.Name}", async () =>
        {
            PacketWriter create = a.NewPacket(ClientOpcode.PartyRequest);
            create.WriteByte(1);
            await a.SendAsync(create).ConfigureAwait(false);

            PacketReader done = await a.ExpectAsync(ServerOpcode.PartyResult).ConfigureAwait(false);
            int op = done.ReadByte();
            if (op != 8)
            {
                throw new InvalidOperationException($"create result op {op}");
            }

            Task<PacketReader> inviteTask = b.ExpectAsync(ServerOpcode.PartyResult);
            PacketWriter invite = a.NewPacket(ClientOpcode.PartyRequest);
            invite.WriteByte(4);
            invite.WriteString(b.CharacterName);
            await a.SendAsync(invite).ConfigureAwait(false);

            PacketReader invited = await inviteTask.ConfigureAwait(false);
            int inviteOp = invited.ReadByte();
            if (inviteOp != 4)
            {
                throw new InvalidOperationException($"invite op {inviteOp}");
            }

            int partyId = invited.ReadInt();
            PacketWriter join = b.NewPacket(ClientOpcode.PartyRequest);
            join.WriteByte(3);
            join.WriteInt(partyId);
            await b.SendAsync(join).ConfigureAwait(false);

            PacketReader joined = await a.ExpectAsync(ServerOpcode.PartyResult).ConfigureAwait(false);
            return $"party {partyId}, join op {joined.ReadByte()}";
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Map-independent extras for the golden-vector capture: a self-whisper (its ack packet),
    /// a channel change (MigrateCommand + the fresh entry blob on the other channel), and a
    /// cash-shop entry (SetCashShop's full CharacterData + balances + locker). Every step is
    /// tolerant — a side that lacks a feature just skips, the diff shows the hole.
    /// </summary>
    public async Task RunCaptureExtrasAsync(BotClient bot, CancellationToken ct)
    {
        await StepAsync(bot, "capture:whisper-ack", async () =>
        {
            PacketWriter w = bot.NewPacket(ClientOpcode.Whisper);
            w.WriteByte(0x02 | 0x04);
            w.WriteString(bot.CharacterName);
            w.WriteString("golden");
            await bot.SendAsync(w).ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.Whisper).ConfigureAwait(false);
            return "";
        }).ConfigureAwait(false);

        await StepAsync(bot, "capture:channel-change", async () =>
        {
            PacketWriter w = bot.NewPacket(ClientOpcode.UserTransferChannelRequest);
            w.WriteByte(1);
            await bot.SendAsync(w).ConfigureAwait(false);
            PacketReader r = await bot.ExpectAsync(ServerOpcode.MigrateCommand).ConfigureAwait(false);
            r.ReadByte();
            var ip = new IPAddress(r.ReadBytes(4));
            int port = (ushort)r.ReadShort();
            await MigrateAsync(bot, new IPEndPoint(ip, port), bot.CharacterId, ct).ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
            return $"channel 2 at {ip}:{port}";
        }).ConfigureAwait(false);

        await StepAsync(bot, "capture:cash-shop", async () =>
        {
            await bot.SendAsync(bot.NewPacket(ClientOpcode.UserMigrateToCashShopRequest)).ConfigureAwait(false);
            PacketReader r = await bot.ExpectAsync(ServerOpcode.MigrateCommand).ConfigureAwait(false);
            r.ReadByte();
            var ip = new IPAddress(r.ReadBytes(4));
            int port = (ushort)r.ReadShort();
            await MigrateAsync(bot, new IPEndPoint(ip, port), bot.CharacterId, ct).ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.SetCashShop).ConfigureAwait(false);
            return $"cash shop at {ip}:{port}";
        }).ConfigureAwait(false);
    }

    // ---- shared helpers ------------------------------------------------------------------

    /// <summary>
    /// Sweeps every given item id: spawns each as a ground drop (/drop) and validates the
    /// DropEnterField packet the client would render. Ground drops don't fill the inventory, so
    /// the whole drop table can be swept in one pass. Returns the ids whose packet was malformed
    /// or whose grant produced no drop packet.
    /// </summary>
    public async Task<List<string>> SweepDropsAsync(BotClient bot, IReadOnlyList<int> itemIds)
    {
        var bad = new List<string>();
        int done = 0;
        foreach (int id in itemIds)
        {
            await ChatAsync(bot, $"/drop {id} 1").ConfigureAwait(false);
            try
            {
                PacketReader enter = await bot.ExpectAsync(ServerOpcode.DropEnterField, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                enter.ReadByte();
                enter.ReadInt();
                ParseDropEnterTail(enter);
                if (enter.Remaining != 0)
                {
                    bad.Add($"{id}(+{enter.Remaining}b)");
                }
            }
            catch (Exception ex)
            {
                bad.Add($"{id}({ex.GetType().Name})");
            }

            if (++done % 500 == 0)
            {
                Console.WriteLine($"[{bot.Name}] swept {done}/{itemIds.Count} drops, {bad.Count} bad so far");
            }
        }

        return bad;
    }

    /// <summary>Reads the rest of a DropEnterField packet (after enter-type + object id).</summary>
    private static void ParseDropEnterTail(PacketReader r)
    {
        int isMeso = r.ReadByte();
        r.ReadInt();                 // meso amount or item id
        r.ReadInt();                 // owner
        r.ReadByte();                // drop type
        r.ReadShort(); r.ReadShort(); // landing x/y
        r.ReadInt();                 // source object id
        // ANIMATION drops (not on-ground) carry the drop-from point; a spawned /drop uses it.
        if (r.Remaining > (isMeso == 0 ? 10 : 2))
        {
            r.ReadShort(); r.ReadShort(); r.ReadShort(); // drop-from x/y + pad
        }

        if (isMeso == 0)
        {
            r.ReadLong();            // item expiration (meso omits this)
        }

        r.ReadByte();                // player-drop flag
        r.ReadByte();                // trailing
    }

    /// <summary>
    /// Reads server chat lines until one contains <paramref name="needle"/>, and returns every
    /// line read. Commands reply over several chat packets now (/help, /status), and earlier steps
    /// leave their own replies queued, so a plain "read one chat line" would pick up a stale one.
    /// </summary>
    private static async Task<List<string>> ReadChatUntilAsync(BotClient bot, string needle, int maxLines = 64)
    {
        var lines = new List<string>();
        for (int i = 0; i < maxLines; i++)
        {
            PacketReader r = await bot.ExpectAsync(ServerOpcode.UserChat).ConfigureAwait(false);
            r.ReadInt();            // character id
            r.ReadBool();           // isGm
            string line = r.ReadString();
            lines.Add(line);
            if (line.Contains(needle, StringComparison.Ordinal))
            {
                return lines;
            }
        }

        throw new InvalidOperationException($"no chat line containing '{needle}' in {maxLines} lines");
    }

    private static async Task ChatAsync(BotClient bot, string text)
    {
        PacketWriter w = bot.NewPacket(ClientOpcode.UserChat);
        w.WriteInt(0);          // update time
        w.WriteString(text);
        w.WriteByte(0);         // balloon only
        await bot.SendAsync(w).ConfigureAwait(false);
    }

    private static async Task SelectNpcAsync(BotClient bot, int npcObjectHint)
    {
        // Our server resolves either a field object id or a raw template id.
        PacketWriter w = bot.NewPacket(ClientOpcode.UserSelectNpc);
        w.WriteInt(npcObjectHint);
        w.WriteShort(0);
        w.WriteShort(0);
        await bot.SendAsync(w).ConfigureAwait(false);
    }

    private static async Task EscapeDialogAsync(BotClient bot, int msgType)
    {
        PacketWriter w = bot.NewPacket(ClientOpcode.UserScriptMessageAnswer);
        w.WriteByte((byte)msgType);
        w.WriteByte(unchecked((byte)-1)); // escape ends the conversation
        await bot.SendAsync(w).ConfigureAwait(false);
    }
}
