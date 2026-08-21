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
(IAccountRepository accounts, ICharacterRepository characters, IStorageRepository? storageRepo, IKeymapRepository? keymapRepo, IGuildRepository? guildRepo) = CreateRepositories();
var loginService = new LoginService(accounts, autoRegister: true);

// The login server hands clients to the channel via LP_SelectCharacterResult. The IP it
// advertises must be one the client can actually reach: loopback for local play, or the
// server's LAN/public IP (set CRONUS_HOST=<ip-or-hostname>) so friends can connect on a
// fixed IP. JMS sends the address as 4 bytes, so this resolves to IPv4.
IPAddress channelHost = ResolveHost(Environment.GetEnvironmentVariable("CRONUS_HOST"), IPAddress.Loopback);
var channelEndpoint = new IPEndPoint(channelHost, channelPort);

// LP_AliveReq body: the server pings idle clients so they keep the connection open.
byte[] keepAlive = new PacketWriter(
    serverOps.Get(ServerOpcode.AliveReq), config.PacketHeaderSize, config.CodePage).ToArray();

// Wire diagnostics: log every packet the server sends (opcode + length + hex).
MapleSession.DebugOnSend = (role, body) =>
{
    ReadOnlySpan<byte> b = body.Span;
    int opcode = b.Length >= 2 ? b[0] | (b[1] << 8) : -1;
    Console.WriteLine($"[send:{role}] opcode 0x{opcode:X4} ({b.Length} bytes): {Convert.ToHexString(b)}");
};

int startMap = int.TryParse(Environment.GetEnvironmentVariable("CRONUS_STARTMAP"), out int sm) ? sm : 100000000;
Console.WriteLine($"[map] new characters start in map {startMap}");

var loginListener = new MapleListener(
    new IPEndPoint(IPAddress.Any, loginPort),
    config,
    () => new LoggingHandler(
        new LoginHandler(clientOps, serverOps, loginService, config,
            characters: characters, channelEndpoint: channelEndpoint, startMapId: startMap),
        "login"),
    keepAlive: null); // keep-alive disabled during login diagnosis

// Map data from a wz_xml tree if CRONUS_WZ points at one, else no static map data (portal-by-
// name transfers degrade to "disabled portal"; direct map-id jumps still work; no NPCs spawn).
IMapProvider maps = CreateMapProvider();
IMobProvider mobs = CreateMobProvider();
var fields = new FieldRegistry(maps, mobs);
ISkillProvider skills = CreateSkillProvider();
IItemProvider items = CreateItemProvider();
IDropProvider drops = CreateDropProvider();
IShopProvider shops = CreateShopProvider();
IQuestProvider quests = CreateQuestProvider();
Rates rates = CreateRates();

// NPC scripts from CRONUS_SCRIPTS/npc/{id}.js and portal scripts from CRONUS_SCRIPTS/portal/{name}.js.
NpcScriptEngine? npcScripts = CreateNpcScriptEngine();
PortalScriptEngine? portalScripts = CreatePortalScriptEngine();

// Shared across all connections so messenger/party windows tie players together across fields.
var messengers = new MessengerRegistry(new ChannelPackets(serverOps, config));
var parties = new PartyRegistry();
var storages = new StorageRegistry(storageRepo);
var keymaps = new KeymapRegistry(keymapRepo);
var trades = new TradeRegistry();
var buffs = new BuffTracker();
var guilds = new GuildRegistry(guildRepo);
var miniGames = new MiniGameRegistry();

var channelListener = new MapleListener(
    new IPEndPoint(IPAddress.Any, channelPort),
    config,
    () => new LoggingHandler(
        new ChannelHandler(clientOps, serverOps, characters, config, fields, maps, npcScripts, skills, channelId: 0, messengers: messengers, parties: parties, portalScripts: portalScripts, items: items, drops: drops, shops: shops, storages: storages, keymaps: keymaps, quests: quests, rates: rates, trades: trades, buffs: buffs, guilds: guilds, miniGames: miniGames),
        "channel"),
    keepAlive);

// Server ticks: respawn dead mobs, regenerate idle players' HP/MP, and periodically persist
// online characters so a crash loses at most a couple of minutes of progress.
var mobRespawn = new MobRespawnService(fields, new ChannelPackets(serverOps, config));
var playerRegen = new PlayerRegenService(fields, new ChannelPackets(serverOps, config), parties);
var autoSave = new CharacterAutoSaveService(fields, characters);
var buffExpiry = new BuffExpiryService(fields, buffs, new ChannelPackets(serverOps, config));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Cronus — JMS v{config.Version}, region {config.Region}");
Console.WriteLine($"  login   : listening on 0.0.0.0:{loginPort}");
Console.WriteLine($"  channel : listening on 0.0.0.0:{channelPort}, advertised to clients as {channelEndpoint}");
if (channelHost.Equals(IPAddress.Loopback))
{
    Console.WriteLine("  (localhost only — set CRONUS_HOST=<your LAN/public IP> so friends can connect)");
}
Console.WriteLine("Accounts auto-register on first login. Press Ctrl+C to stop.");

try
{
    await Task.WhenAll(
        loginListener.RunAsync(cts.Token),
        channelListener.RunAsync(cts.Token),
        mobRespawn.RunAsync(cts.Token),
        playerRegen.RunAsync(cts.Token),
        autoSave.RunAsync(cts.Token),
        buffExpiry.RunAsync(cts.Token));
}
finally
{
    await loginListener.DisposeAsync();
    await channelListener.DisposeAsync();
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

static IMapProvider CreateMapProvider()
{
    string? wzRoot = Environment.GetEnvironmentVariable("CRONUS_WZ");
    if (string.IsNullOrWhiteSpace(wzRoot) || !Directory.Exists(wzRoot))
    {
        Console.WriteLine("[wz] CRONUS_WZ not set — no static map data (portal-by-name disabled).");
        return new InMemoryMapProvider(Array.Empty<MapData>());
    }

    Console.WriteLine($"[wz] Loading map data on demand from {wzRoot}");
    return new WzMapProvider(wzRoot);
}

static IMobProvider CreateMobProvider()
{
    string? wzRoot = Environment.GetEnvironmentVariable("CRONUS_WZ");
    if (string.IsNullOrWhiteSpace(wzRoot) || !Directory.Exists(wzRoot))
    {
        return new InMemoryMobProvider(Array.Empty<MobData>());
    }

    return new WzMobProvider(wzRoot);
}

static ISkillProvider CreateSkillProvider()
{
    string? wzRoot = Environment.GetEnvironmentVariable("CRONUS_WZ");
    if (string.IsNullOrWhiteSpace(wzRoot) || !Directory.Exists(wzRoot))
    {
        return NullSkillProvider.Instance; // no wz data → skills uncapped
    }

    return new WzSkillProvider(wzRoot);
}

static IItemProvider CreateItemProvider()
{
    string? wzRoot = Environment.GetEnvironmentVariable("CRONUS_WZ");
    if (string.IsNullOrWhiteSpace(wzRoot) || !Directory.Exists(wzRoot))
    {
        return new InMemoryItemProvider(Array.Empty<ConsumeSpec>()); // no wz → no item effects
    }

    return new WzItemProvider(wzRoot);
}

// Mob drop tables from the reference drop_data.sql dump if CRONUS_DROPS points at one, else no
// tables (mobs fall back to a small placeholder meso pile). The dump is the same asset the Java
// build loads into its drop_data table.
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

// Quest definitions from the wz tree's Quest/Check.img.xml + Act.img.xml (CRONUS_WZ), else no
// quest data (quest accept/complete degrade to script-driven only).
static IQuestProvider CreateQuestProvider()
{
    string? wzRoot = Environment.GetEnvironmentVariable("CRONUS_WZ");
    if (string.IsNullOrWhiteSpace(wzRoot) || !Directory.Exists(wzRoot))
    {
        return new InMemoryQuestProvider(Array.Empty<QuestData>());
    }

    return new WzQuestProvider(wzRoot);
}

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

static (IAccountRepository, ICharacterRepository, IStorageRepository?, IKeymapRepository?, IGuildRepository?) CreateRepositories()
{
    string? connectionString = Environment.GetEnvironmentVariable("CRONUS_DB");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        Console.WriteLine("[db] CRONUS_DB not set — using in-memory stores (not persistent).");
        return (new InMemoryAccountRepository(), new InMemoryCharacterRepository(), null, null, null);
    }

    try
    {
        Func<CronusDbContext> factory = MySqlDatabase.CreateFactory(connectionString);
        MySqlDatabase.EnsureCreated(factory);
        Console.WriteLine("[db] Connected to MySQL; accounts, characters, storage, keymaps, and guilds are persistent.");
        return (
            new DbAccountRepository(factory),
            new DbCharacterRepository(factory),
            new DbStorageRepository(factory),
            new DbKeymapRepository(factory),
            new DbGuildRepository(factory));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[db] MySQL unavailable ({ex.Message}); falling back to in-memory stores.");
        return (new InMemoryAccountRepository(), new InMemoryCharacterRepository(), null, null, null);
    }
}
