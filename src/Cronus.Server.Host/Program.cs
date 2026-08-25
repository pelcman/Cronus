using System.Net;
using Cronus.Common;
using Cronus.Data;
using Cronus.Database;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;
using Cronus.Server.Channel;
using Cronus.Server.Host;
using Cronus.Server.Login;

// JMS v186 server host: login server + one channel server in a single process.
// Point a JMS v186 client (via EmuClient localhost redirect) at the login port.

// Configuration comes from environment variables; a repo-root `.env` file feeds them
// (Maple2's approach — see .env.example). Real environment variables override the file.
string? envFile = DotEnv.Load();

// Every console line also lands in logs/cronus-<timestamp>.log (CRONUS_LOG_DIR overrides/disables).
string? logFile = TeeLog.Attach();

Console.WriteLine(envFile is null
    ? "[env] no .env file found — using process environment only (see .env.example)."
    : $"[env] loaded {envFile}");
if (logFile is not null)
{
    Console.WriteLine($"[log] mirroring console to {logFile}");
}

CodePage.Register();
ServerConfig config = ServerConfig.Jms186;

int loginPort = args.Length > 0 && int.TryParse(args[0], out int p) ? p : 8484;
int channelPort = args.Length > 1 && int.TryParse(args[1], out int cp) ? cp : 7575;

string opcodeDir = Path.Combine(AppContext.BaseDirectory, "opcodes");
OpcodeTable clientOps = OpcodeTable.LoadFile(Path.Combine(opcodeDir, "JMS_v186_ClientPacket.properties"));
OpcodeTable serverOps = OpcodeTable.LoadFile(Path.Combine(opcodeDir, "JMS_v186_ServerPacket.properties"));

WarnUnresolved("client", clientOps);
WarnUnresolved("server", serverOps);

// Use MySQL when a connection string is configured (CRONUS_DB env var), else fall back to the
// in-memory stores so the server runs with zero external dependencies for local testing.
(IAccountRepository accounts, ICharacterRepository characters, IStorageRepository? storageRepo, IKeymapRepository? keymapRepo, IGuildRepository? guildRepo, IHiredMerchantRepository? merchantRepo, IParcelRepository parcelRepo) = CreateRepositories();
// Accounts auto-register on first login unless CRONUS_AUTO_REGISTER disables it (0/false/off).
bool autoRegister = Environment.GetEnvironmentVariable("CRONUS_AUTO_REGISTER")?.Trim().ToLowerInvariant()
    is not ("0" or "false" or "off" or "no");
Console.WriteLine(autoRegister
    ? "[login] auto-register ON — unknown accounts are created on first login."
    : "[login] auto-register OFF — only existing accounts can log in.");
var loginService = new LoginService(accounts, autoRegister: autoRegister);

// The login server hands clients to the channel via LP_SelectCharacterResult. The IP it
// advertises must be one the client can actually reach: loopback for local play, or the
// server's LAN/public IP (set CRONUS_HOST=<ip-or-hostname>) so friends can connect on a
// fixed IP. JMS sends the address as 4 bytes, so this resolves to IPv4.
IPAddress channelHost = ResolveHost(Environment.GetEnvironmentVariable("CRONUS_HOST"), IPAddress.Loopback);
var channelEndpoint = new IPEndPoint(channelHost, channelPort);

// CRONUS_CHANNELS game channels (default 2), on consecutive ports from the channel port.
int channelCount = Math.Clamp(
    int.TryParse(Environment.GetEnvironmentVariable("CRONUS_CHANNELS"), out int cc) ? cc : 2, 1, 8);
IReadOnlyList<IPEndPoint> channelEndpoints =
    Enumerable.Range(0, channelCount).Select(i => new IPEndPoint(channelHost, channelPort + i)).ToList();

// The cash-shop server sits on the next port after the channels. CRONUS_NX keeps every
// account's NX balance topped up to this floor on entry (default 300000; 0 disables the shop).
int cashShopPort = channelPort + channelCount;
var cashShopEndpoint = new IPEndPoint(channelHost, cashShopPort);
int nxFloor = int.TryParse(Environment.GetEnvironmentVariable("CRONUS_NX"), out int nx)
    ? Math.Max(0, nx)
    : 300_000;
bool cashShopEnabled = nxFloor > 0;

// LP_AliveReq body: the server pings idle clients so they keep the connection open.
byte[] keepAlive = new PacketWriter(
    serverOps.Get(ServerOpcode.AliveReq), config.PacketHeaderSize, config.CodePage).ToArray();

// Wire diagnostics (CRONUS_DEBUG=1): hex-dump every sent packet and log every received
// opcode. Off by default — console I/O this heavy slows real multi-player sessions.
bool wireDebug = Environment.GetEnvironmentVariable("CRONUS_DEBUG")?.Trim().ToLowerInvariant()
    is "1" or "true" or "on" or "yes";
if (wireDebug)
{
    Console.WriteLine("[debug] wire diagnostics ON — every packet is logged (CRONUS_DEBUG).");
    MapleSession.DebugOnSend = (role, body) =>
    {
        ReadOnlySpan<byte> b = body.Span;
        int opcode = b.Length >= 2 ? b[0] | (b[1] << 8) : -1;
        Console.WriteLine($"[send:{role}] opcode 0x{opcode:X4} ({b.Length} bytes): {Convert.ToHexString(b)}");
    };
}

int startMap = int.TryParse(Environment.GetEnvironmentVariable("CRONUS_STARTMAP"), out int sm) ? sm : 100000000;
Console.WriteLine($"[map] new characters start in map {startMap}");

var loginListener = new MapleListener(
    new IPEndPoint(IPAddress.Any, loginPort),
    config,
    () => new LoggingHandler(
        new LoginHandler(clientOps, serverOps, loginService, config,
            worlds: WorldRegistry.CreateDefault(channelCount),
            characters: characters, channelEndpoint: channelEndpoint, startMapId: startMap,
            channelEndpoints: channelEndpoints),
        "login", verbose: wireDebug),
    keepAlive: null); // keep-alive disabled during login diagnosis

// Game data (maps/mobs/items/skills/quests/strings/styles): resolved once, in preference order.
//   CRONUS_GAMEDATA  → the gamedata.db built by Cronus.Ingest (single file, from the client)
//   CRONUS_CLIENT    → a client folder with .wz files; gamedata.db is built from it on first
//                      boot (and reused after), so pointing at the client is all it takes
//   CRONUS_WZ        → the classic loose wz_xml dump tree
// With none of them, the server runs data-less (walkable maps, no NPCs/mobs/quest data).
IWzStore? wzStore = ResolveWzStore();
IMapProvider maps = wzStore is null ? new InMemoryMapProvider(Array.Empty<MapData>()) : new WzMapProvider(wzStore);
IMobProvider mobs = wzStore is null ? new InMemoryMobProvider(Array.Empty<MobData>()) : new WzMobProvider(wzStore);

// Each channel gets its own world state (fields); everything account-scoped is shared below.
var channelFields = new List<FieldRegistry>();
for (int i = 0; i < channelCount; i++)
{
    channelFields.Add(new FieldRegistry(maps, mobs));
}

FieldRegistry fields = channelFields[0];
ISkillProvider skills = wzStore is null ? NullSkillProvider.Instance : new WzSkillProvider(wzStore);
IItemProvider items = wzStore is null ? new InMemoryItemProvider(Array.Empty<ConsumeSpec>()) : new WzItemProvider(wzStore);
IDropProvider drops = CreateDropProvider();
IShopProvider shops = CreateShopProvider();
IQuestProvider quests = wzStore is null
    ? new InMemoryQuestProvider(Array.Empty<QuestData>())
    : new WzQuestProvider(wzStore);
IReactorProvider? reactorProvider = wzStore is null ? null : new WzReactorProvider(wzStore);
Console.WriteLine(wzStore is null
    ? "[quests] no game data — quests/reactors disabled."
    : "[quests] quest definitions + reactors ready.");
IReactorDropProvider reactorDrops = CreateReactorDropProvider();

// Every named item, grouped by category — powers /dbgshop. Needs the wz String tables.
IItemCatalog? itemCatalog = wzStore is null ? null : new WzItemCatalog(wzStore);
Console.WriteLine(itemCatalog is null
    ? "[shops] item catalog unavailable (no game data) — /dbgshop disabled."
    : "[shops] item catalog ready — /dbgshop lists every item by category.");

// Every named map, grouped by region — powers /dbgwarp. Same wz String requirement.
IMapCatalog? mapCatalog = wzStore is null ? null : new WzMapCatalog(wzStore);
Console.WriteLine(mapCatalog is null
    ? "[maps] map catalog unavailable (no game data) — /dbgwarp disabled."
    : "[maps] map catalog ready — /dbgwarp lists every map by region.");
INpcNameProvider? npcNames = wzStore is null ? null : new WzNpcNameProvider(wzStore);
IQuestNpcIndex? questNpcs = wzStore is null ? null : new WzQuestNpcIndex(wzStore);
IStyleProvider? styles = wzStore is null ? null : new WzStyleProvider(wzStore);
ICommodityProvider? commodities = wzStore is null ? null : new WzCommodityProvider(wzStore);
Rates rates = CreateRates();

// NPC scripts from CRONUS_SCRIPTS/npc/{id}.js and portal scripts from CRONUS_SCRIPTS/portal/{name}.js.
NpcScriptEngine? npcScripts = CreateNpcScriptEngine();
PortalScriptEngine? portalScripts = CreatePortalScriptEngine();
PortalScriptEngine? reactorScripts = CreateReactorScriptEngine();

// Shared across all connections so messenger/party windows tie players together across fields.
var messengers = new MessengerRegistry(new ChannelPackets(serverOps, config));
var parties = new PartyRegistry();
var storages = new StorageRegistry(storageRepo);
var keymaps = new KeymapRegistry(keymapRepo);
var trades = new TradeRegistry();
var buffs = new BuffTracker();
var guilds = new GuildRegistry(guildRepo);
var miniGames = new MiniGameRegistry();
var playerShops = new PlayerShopRegistry();
var merchants = new HiredMerchantRegistry(merchantRepo);

var channelListeners = new List<MapleListener>();
for (int i = 0; i < channelCount; i++)
{
    int channelId = i;
    FieldRegistry chFields = channelFields[i];
    channelListeners.Add(new MapleListener(
        new IPEndPoint(IPAddress.Any, channelPort + i),
        config,
        () => new LoggingHandler(
            new ChannelHandler(clientOps, serverOps, characters, config, chFields, maps, npcScripts, skills, channelId: channelId, messengers: messengers, parties: parties, portalScripts: portalScripts, items: items, drops: drops, shops: shops, storages: storages, keymaps: keymaps, quests: quests, rates: rates, trades: trades, buffs: buffs, guilds: guilds, miniGames: miniGames, playerShops: playerShops, merchants: merchants, reactors: reactorProvider, reactorDrops: reactorDrops, reactorScripts: reactorScripts, accounts: accounts, itemCatalog: itemCatalog, mapCatalog: mapCatalog, questNpcs: questNpcs, parcels: parcelRepo, npcNames: npcNames, styles: styles, channelEndpoints: channelEndpoints, worldFields: channelFields, cashShopEndpoint: cashShopEnabled ? cashShopEndpoint : null),
            $"channel{channelId}", verbose: wireDebug),
        keepAlive));
}

// The cash-shop server (its own listener; the client migrates here and back).
MapleListener? cashShopListener = null;
if (cashShopEnabled)
{
    cashShopListener = new MapleListener(
        new IPEndPoint(IPAddress.Any, cashShopPort),
        config,
        () => new LoggingHandler(
            new CashShopHandler(clientOps, serverOps, characters, accounts, config,
                commodities: commodities, channelEndpoints: channelEndpoints, nxFloor: nxFloor),
            "cashshop", verbose: wireDebug),
        keepAlive);
    Console.WriteLine($"[cashshop] listening on port {cashShopPort}, NX allowance floor {nxFloor}.");
}
else
{
    Console.WriteLine("[cashshop] disabled (CRONUS_NX=0) — the client button is declined.");
}

// Server ticks per channel: respawn dead mobs, regenerate idle players' HP/MP, and
// periodically persist online characters so a crash loses at most a couple of minutes.
var tickTasks = new List<Func<CancellationToken, Task>>();
foreach (FieldRegistry chFields in channelFields)
{
    var mobRespawnSvc = new MobRespawnService(chFields, new ChannelPackets(serverOps, config));
    var playerRegenSvc = new PlayerRegenService(chFields, new ChannelPackets(serverOps, config), parties);
    var autoSaveSvc = new CharacterAutoSaveService(chFields, characters);
    var buffExpirySvc = new BuffExpiryService(chFields, buffs, new ChannelPackets(serverOps, config));
    tickTasks.Add(mobRespawnSvc.RunAsync);
    tickTasks.Add(playerRegenSvc.RunAsync);
    tickTasks.Add(autoSaveSvc.RunAsync);
    tickTasks.Add(buffExpirySvc.RunAsync);
}

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Cronus — JMS v{config.Version}, region {config.Region}");
Console.WriteLine($"  login   : listening on 0.0.0.0:{loginPort}");
Console.WriteLine($"  channels: {channelCount} — ports {channelPort}..{channelPort + channelCount - 1}, advertised to clients as {channelHost}");
if (channelHost.Equals(IPAddress.Loopback))
{
    Console.WriteLine("  (localhost only — set CRONUS_HOST=<your LAN/public IP> so friends can connect)");
}
Console.WriteLine("Accounts auto-register on first login. Press Ctrl+C to stop.");

try
{
    var tasks = new List<Task> { loginListener.RunAsync(cts.Token) };
    tasks.AddRange(channelListeners.Select(l => l.RunAsync(cts.Token)));
    if (cashShopListener is not null)
    {
        tasks.Add(cashShopListener.RunAsync(cts.Token));
    }

    tasks.AddRange(tickTasks.Select(run => run(cts.Token)));
    await Task.WhenAll(tasks);
}
finally
{
    await loginListener.DisposeAsync();
    foreach (MapleListener listener in channelListeners)
    {
        await listener.DisposeAsync();
    }

    if (cashShopListener is not null)
    {
        await cashShopListener.DisposeAsync();
    }
}

Console.WriteLine("Stopped.");

// Resolves CRONUS_HOST (an IPv4 literal or a hostname) to the address advertised to clients
// for the channel connection. Empty/unset or unresolvable → fallback (loopback).
static IPAddress ResolveHost(string? host, IPAddress fallback)
{
    if (string.IsNullOrWhiteSpace(host))
    {
        return fallback;
    }

    if (IPAddress.TryParse(host, out IPAddress? literal))
    {
        return literal;
    }

    try
    {
        foreach (IPAddress addr in Dns.GetHostAddresses(host))
        {
            if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return addr; // JMS advertises the channel IP as 4 bytes, so prefer IPv4
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[net] could not resolve CRONUS_HOST='{host}' ({ex.Message}); using {fallback}");
    }

    return fallback;
}





// Mob drop tables from the reference drop_data.sql dump if CRONUS_DROPS points at one, else no
// tables (mobs fall back to a small placeholder meso pile). The dump is the same asset the Java
// build loads into its drop_data table.

static IWzStore? ResolveWzStore()
{
    // 1) An explicit game-data database.
    string? gamedata = Environment.GetEnvironmentVariable("CRONUS_GAMEDATA");
    if (!string.IsNullOrWhiteSpace(gamedata) && File.Exists(gamedata))
    {
        var store = new SqliteWzStore(gamedata);
        Console.WriteLine($"[data] game data from {gamedata} " +
            $"(ingested {store.Meta.GetValueOrDefault("ingested_utc", "?")} from {store.Meta.GetValueOrDefault("source", "?")})");
        return store;
    }

    // 2) A client folder: build (or reuse) gamedata.db right next to the working directory.
    string? clientDir = Environment.GetEnvironmentVariable("CRONUS_CLIENT");
    if (!string.IsNullOrWhiteSpace(clientDir) && Directory.Exists(clientDir))
    {
        string dbPath = string.IsNullOrWhiteSpace(gamedata) ? "gamedata.db" : gamedata;
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"[data] building {dbPath} from {clientDir} (first boot; ~20s)...");
            Cronus.Data.Wz.WzIngest.BuildDatabase(clientDir, dbPath, Console.WriteLine);
        }

        Console.WriteLine($"[data] game data from {dbPath} (client: {clientDir})");
        return new SqliteWzStore(dbPath);
    }

    // 2b) No env at all, but a previously built gamedata.db sits in the working directory.
    if (string.IsNullOrWhiteSpace(gamedata) && File.Exists("gamedata.db"))
    {
        Console.WriteLine("[data] game data from ./gamedata.db");
        return new SqliteWzStore("gamedata.db");
    }

    // 3) The classic loose wz_xml dump tree.
    string? wzRoot = Environment.GetEnvironmentVariable("CRONUS_WZ");
    if (!string.IsNullOrWhiteSpace(wzRoot) && Directory.Exists(wzRoot))
    {
        Console.WriteLine($"[data] game data from wz_xml tree {wzRoot}");
        return new DirectoryWzStore(wzRoot);
    }

    Console.WriteLine("[data] no game data (set CRONUS_GAMEDATA, CRONUS_CLIENT, or CRONUS_WZ) — maps are walkable but empty.");
    return null;
}

static IDropProvider CreateDropProvider()
{
    string? dropFile = Environment.GetEnvironmentVariable("CRONUS_DROPS");
    if (string.IsNullOrWhiteSpace(dropFile) || !File.Exists(dropFile))
    {
        Console.WriteLine("[drops] CRONUS_DROPS not set — mobs drop placeholder meso only.");
        return new InMemoryDropProvider(new Dictionary<int, IReadOnlyList<DropEntry>>());
    }

    SqlDropProvider provider = SqlDropProvider.LoadFile(dropFile);
    Console.WriteLine($"[drops] loaded drop tables from {dropFile}");
    return provider;
}

// Server rates from CRONUS_RATE_EXP / CRONUS_RATE_DROP / CRONUS_RATE_MESO (default 1.0 = authentic).
static Rates CreateRates()
{
    static double Rate(string name)
        => double.TryParse(Environment.GetEnvironmentVariable(name), out double v) && v > 0 ? v : 1.0;

    var rates = new Rates(Rate("CRONUS_RATE_EXP"), Rate("CRONUS_RATE_DROP"), Rate("CRONUS_RATE_MESO"));
    if (rates != Rates.Default)
    {
        Console.WriteLine($"[rates] exp x{rates.Exp}, drop x{rates.Drop}, meso x{rates.Meso}");
    }

    return rates;
}

// quest data (quest accept/complete degrade to script-driven only).

// NPC shops from a shops+shopitems SQL dump if CRONUS_SHOPS points at one, else no shops (vendor
// NPCs open nothing). The dump is the same asset the Java build loads into its shops tables.
static IShopProvider CreateShopProvider()
{
    string? shopFile = Environment.GetEnvironmentVariable("CRONUS_SHOPS");
    if (string.IsNullOrWhiteSpace(shopFile) || !File.Exists(shopFile))
    {
        Console.WriteLine("[shops] CRONUS_SHOPS not set — NPC shops disabled.");
        return new InMemoryShopProvider(Array.Empty<Shop>());
    }

    SqlShopProvider provider = SqlShopProvider.LoadFile(shopFile);
    Console.WriteLine($"[shops] loaded shops from {shopFile}");
    return provider;
}

// Reactor drop tables ride in the same init_data_set.sql dump the shops come from.
static IReactorDropProvider CreateReactorDropProvider()
{
    string? shopFile = Environment.GetEnvironmentVariable("CRONUS_SHOPS");
    if (string.IsNullOrWhiteSpace(shopFile) || !File.Exists(shopFile))
    {
        return new InMemoryReactorDropProvider(new Dictionary<int, IReadOnlyList<ReactorDropEntry>>());
    }

    SqlReactorDropProvider provider = SqlReactorDropProvider.LoadFile(shopFile);
    Console.WriteLine("[reactor] reactor drop tables loaded (reactordrops).");
    return provider;
}

static NpcScriptEngine? CreateNpcScriptEngine()
{
    string? scriptRoot = Environment.GetEnvironmentVariable("CRONUS_SCRIPTS");
    if (string.IsNullOrWhiteSpace(scriptRoot))
    {
        Console.WriteLine("[npc] CRONUS_SCRIPTS not set — NPC dialogs disabled.");
        return null;
    }

    string npcDir = Path.Combine(scriptRoot, "npc");
    string questDir = Path.Combine(scriptRoot, "quest");
    Console.WriteLine($"[npc] Loading NPC scripts on demand from {npcDir} (quest scripts from {questDir})");
    return new NpcScriptEngine(new FolderNpcScriptSource(npcDir), new FolderNpcScriptSource(questDir));
}





static PortalScriptEngine? CreateReactorScriptEngine()
{
    string? scriptRoot = Environment.GetEnvironmentVariable("CRONUS_SCRIPTS");
    if (string.IsNullOrWhiteSpace(scriptRoot))
    {
        return null;
    }

    return new PortalScriptEngine(new FolderPortalScriptSource(Path.Combine(scriptRoot, "reactor")));
}

static PortalScriptEngine? CreatePortalScriptEngine()
{
    string? scriptRoot = Environment.GetEnvironmentVariable("CRONUS_SCRIPTS");
    if (string.IsNullOrWhiteSpace(scriptRoot))
    {
        return null;
    }

    string portalDir = Path.Combine(scriptRoot, "portal");
    Console.WriteLine($"[portal] Loading portal scripts on demand from {portalDir}");
    return new PortalScriptEngine(new FolderPortalScriptSource(portalDir));
}

static void WarnUnresolved(string which, OpcodeTable table)
{
    if (table.UnresolvedNames.Count > 0)
    {
        Console.WriteLine($"[warn] {which} opcodes unresolved: {string.Join(", ", table.UnresolvedNames)}");
    }
}

static (IAccountRepository, ICharacterRepository, IStorageRepository?, IKeymapRepository?, IGuildRepository?, IHiredMerchantRepository?, IParcelRepository) CreateRepositories()
{
    string? connectionString = Environment.GetEnvironmentVariable("CRONUS_DB");

    // Explicit opt-out: CRONUS_DB=memory keeps everything in process (wiped on restart).
    if (string.Equals(connectionString, "memory", StringComparison.OrdinalIgnoreCase))
    {
        Console.WriteLine("[db] CRONUS_DB=memory — using in-memory stores (not persistent).");
        return (new InMemoryAccountRepository(), new InMemoryCharacterRepository(), null, null, null, null, new InMemoryParcelRepository());
    }

    // A connection string selects MySQL (multi-process / production deployments).
    if (!string.IsNullOrWhiteSpace(connectionString))
    {
        try
        {
            Func<CronusDbContext> factory = MySqlDatabase.CreateFactory(connectionString);
            MySqlDatabase.EnsureCreated(factory);
            Console.WriteLine("[db] Connected to MySQL; accounts, characters, storage, keymaps, and guilds are persistent.");
            return DbRepositories(factory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[db] MySQL unavailable ({ex.Message}); falling back to in-memory stores.");
            return (new InMemoryAccountRepository(), new InMemoryCharacterRepository(), null, null, null, null, new InMemoryParcelRepository());
        }
    }

    // Default: a SQLite file next to the host — zero-setup persistence, so a plain
    // `Cronus.Server.Host.exe` run survives restarts. CRONUS_DB_FILE overrides the path.
    string dbFile = Environment.GetEnvironmentVariable("CRONUS_DB_FILE")
        ?? Path.Combine(AppContext.BaseDirectory, "cronus.db");
    try
    {
        Func<CronusDbContext> factory = SqliteDatabase.CreateFactory(dbFile);
        SqliteDatabase.EnsureCreated(factory);
        Console.WriteLine($"[db] SQLite at {dbFile} — accounts, characters, storage, keymaps, and guilds are persistent.");
        return DbRepositories(factory);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[db] SQLite unavailable ({ex.Message}); falling back to in-memory stores.");
        return (new InMemoryAccountRepository(), new InMemoryCharacterRepository(), null, null, null, null, new InMemoryParcelRepository());
    }
}

static (IAccountRepository, ICharacterRepository, IStorageRepository?, IKeymapRepository?, IGuildRepository?, IHiredMerchantRepository?, IParcelRepository) DbRepositories(Func<CronusDbContext> factory)
    => (
        new DbAccountRepository(factory),
        new DbCharacterRepository(factory),
        new DbStorageRepository(factory),
        new DbKeymapRepository(factory),
        new DbGuildRepository(factory),
        new DbHiredMerchantRepository(factory),
        new DbParcelRepository(factory));
