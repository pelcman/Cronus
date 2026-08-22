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

    public async Task RunSoloSuiteAsync(BotClient bot, CancellationToken ct)
    {
        await StepAsync(bot, "set-field", async () =>
        {
            await bot.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
            return "";
        }).ConfigureAwait(false);

        await StepAsync(bot, "chat:/help", async () =>
        {
            await ChatAsync(bot, "/help").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.UserChat).ConfigureAwait(false);
            return "";
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:/meso+/level", async () =>
        {
            await ChatAsync(bot, "/meso 1000000").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.StatChanged).ConfigureAwait(false);
            await ChatAsync(bot, "/level 30").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.StatChanged).ConfigureAwait(false);
            return "";
        }).ConfigureAwait(false);

        await StepAsync(bot, "cmd:/item", async () =>
        {
            await ChatAsync(bot, "/item 2030004").ConfigureAwait(false);
            await bot.ExpectAsync(ServerOpcode.InventoryOperation).ConfigureAwait(false);
            return "return scroll granted";
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
            await bot.ExpectAsync(ServerOpcode.SetCashShop).ConfigureAwait(false);
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
    }

    // ---- paired scenarios ----------------------------------------------------------------

    /// <summary>Bot A whispers bot B (they can be on different channels).</summary>
    public async Task RunWhisperPairAsync(BotClient a, BotClient b)
    {
        await StepAsync(a, $"whisper->{b.Name}", async () =>
        {
            Task<PacketReader> receive = b.ExpectAsync(ServerOpcode.Whisper);

            PacketWriter w = a.NewPacket(ClientOpcode.Whisper);
            w.WriteByte(0x02 | 0x04);
            w.WriteString(b.CharacterName);
            w.WriteString("hello from " + a.CharacterName);
            await a.SendAsync(w).ConfigureAwait(false);

            await a.ExpectAsync(ServerOpcode.Whisper).ConfigureAwait(false); // sender ack
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

    // ---- shared helpers ------------------------------------------------------------------

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
