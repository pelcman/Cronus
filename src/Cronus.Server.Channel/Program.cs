using System.Net;
using Cronus.Common;
using Cronus.Data;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Scripting;
using Cronus.Server.Channel;
using Cronus.Server.Core;
using Cronus.Server.Core.Grpc;

// The Channel process (Maple2's Maple2.Server.Game): N game channels, the cash shop, and every
// per-field tick (mob respawn, regen, buff expiry, auto-save, airships). At start-up it registers
// its channels with the World, then heartbeats and receives the World's callbacks (world-wide
// broadcasts, forced disconnects, timetable changes) over the subscription stream. The Login
// sends clients here through the World's hand-off; a client the World did not send is dropped.
//
// run-server.bat starts World → Login → Channel; alone: dotnet run --project src/Cronus.Server.Channel [firstPort]

ServerBootstrap boot = ServerBootstrap.Start("channel");
ServerConfig config = boot.Config;
OpcodeTable clientOps = boot.ClientOps;
OpcodeTable serverOps = boot.ServerOps;

int channelPort = args.Length > 0 && int.TryParse(args[0], out int argPort) ? argPort : ServerBootstrap.Int("CRONUS_CHANNEL_PORT", 7575);

Repositories repos = boot.CreateRepositories(importLegacySqlite: true);

// Damage cap (GameConstants defaults: OFF, 50,000,000). CRONUS_DAMAGE_CAP_ENABLED=1/true/on
// turns the per-line clamp on; CRONUS_DAMAGE_CAP sets the ceiling.
GameConstants.DamageCapEnabled = ServerBootstrap.Flag("CRONUS_DAMAGE_CAP_ENABLED", GameConstants.DamageCapEnabled);
if (ServerBootstrap.Int("CRONUS_DAMAGE_CAP", 0) is int capValue and > 0)
{
    GameConstants.DamageCap = capValue;
}

Console.WriteLine(GameConstants.DamageCapEnabled
    ? $"[combat] damage cap ON — {GameConstants.DamageCap:N0} per line"
    : "[combat] damage cap OFF — client-reported damage applied as-is");

// The IP advertised to clients must be one they can reach: loopback for local play, or the
// server's LAN/public IP (CRONUS_HOST) so friends can connect. JMS sends it as 4 bytes (IPv4).
IPAddress channelHost = boot.AdvertisedHost;

// CRONUS_CHANNELS game channels (default 2) on consecutive ports from the channel port; the
// cash shop sits on the next port. CRONUS_NX keeps every account's NX balance topped up to this
// floor on entry (default 300000; 0 disables the shop).
int channelCount = Math.Clamp(ServerBootstrap.Int("CRONUS_CHANNELS", 2), 1, 8);
IReadOnlyList<int> channelPorts = Enumerable.Range(0, channelCount).Select(i => channelPort + i).ToList();
int cashShopPort = channelPort + channelCount;
var cashShopEndpoint = new IPEndPoint(channelHost, cashShopPort);
int nxFloor = Math.Max(0, ServerBootstrap.Int("CRONUS_NX", 300_000));
bool cashShopEnabled = nxFloor > 0;

// ---- World: register before opening any port (Maple2's Game does the same) --------------------
await using var world = new GrpcWorldClient(boot.WorldAddress);
string instanceId = $"{Environment.MachineName}-{Environment.ProcessId}";
RegisterChannelsResponse? registration = await world.RegisterChannelsAsync(instanceId, channelHost, channelPorts, cashShopEnabled ? cashShopPort : null);
if (registration is null)
{
    Console.WriteLine($"[world] World ({boot.WorldAddress}) に接続できません。run-server.bat は World → Login → Channel の順に起動します。World を先に起動するか、CRONUS_WORLD_URI を確認してください。");
    Environment.Exit(3);
    return;
}

List<int> channelIds = registration.ChannelIds.ToList();
ApplyTimetable(registration.Timetable);
Console.WriteLine($"[world] registered with {world.Address} as {instanceId}: channel(s) {string.Join(", ", channelIds.Select(id => id + 1))}, heartbeat every {registration.HeartbeatSeconds}s");

// ---- Game data (maps/mobs/items/skills/quests/strings/styles), resolved once ------------------
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
// (Phase 1b stage B moves these into the World, so channels can be separate processes.)
var messengers = new MessengerRegistry(new ChannelPackets(serverOps, config));
var parties = new PartyRegistry();
var storages = new StorageRegistry(repos.Storage);
var keymaps = new KeymapRegistry(repos.Keymaps);
var trades = new TradeRegistry();
var buffs = new BuffTracker();
var guilds = new GuildRegistry(repos.Guilds);
var miniGames = new MiniGameRegistry();
var playerShops = new PlayerShopRegistry();
var merchants = new HiredMerchantRegistry(repos.Merchants);

byte[] keepAlive = boot.KeepAlivePacket();
var channelListeners = new List<MapleListener>();
for (int i = 0; i < channelCount; i++)
{
    int channelId = channelIds[i];
    FieldRegistry chFields = channelFields[i];
    channelListeners.Add(new MapleListener(
        new IPEndPoint(IPAddress.Any, channelPorts[i]),
        config,
        () => new LoggingHandler(
            new ChannelHandler(clientOps, serverOps, repos.Characters, config, chFields, maps, npcScripts, skills, channelId: channelId, messengers: messengers, parties: parties, portalScripts: portalScripts, items: items, drops: drops, shops: shops, storages: storages, keymaps: keymaps, quests: quests, rates: rates, trades: trades, buffs: buffs, guilds: guilds, miniGames: miniGames, playerShops: playerShops, merchants: merchants, reactors: reactorProvider, reactorDrops: reactorDrops, reactorScripts: reactorScripts, accounts: repos.Accounts, itemCatalog: itemCatalog, mapCatalog: mapCatalog, questNpcs: questNpcs, parcels: repos.Parcels, npcNames: npcNames, styles: styles, worldFields: channelFields, cashShopEndpoint: cashShopEnabled ? cashShopEndpoint : null, world: world),
            $"channel{channelId}", verbose: boot.WireDebug),
        keepAlive));
}

// The cash-shop server (its own listener; the client migrates here and back through the World).
MapleListener? cashShopListener = null;
if (cashShopEnabled)
{
    cashShopListener = new MapleListener(
        new IPEndPoint(IPAddress.Any, cashShopPort),
        config,
        () => new LoggingHandler(
            new CashShopHandler(clientOps, serverOps, repos.Characters, repos.Accounts, config,
                commodities: commodities, nxFloor: nxFloor, world: world),
            "cashshop", verbose: boot.WireDebug),
        keepAlive);
    Console.WriteLine($"[cashshop] listening on port {cashShopPort}, NX allowance floor {nxFloor}.");
}
else
{
    Console.WriteLine("[cashshop] disabled (CRONUS_NX=0) — the client button is declined.");
}

// Server ticks per channel: respawn dead mobs, regenerate idle players' HP/MP, expire buffs, run
// the airships on the World's timetable, and periodically persist online characters so a crash
// loses at most a couple of minutes.
var tickTasks = new List<Func<CancellationToken, Task>>();
foreach (FieldRegistry chFields in channelFields)
{
    tickTasks.Add(new MobRespawnService(chFields, new ChannelPackets(serverOps, config)).RunAsync);
    tickTasks.Add(new PlayerRegenService(chFields, new ChannelPackets(serverOps, config), parties).RunAsync);
    tickTasks.Add(new CharacterAutoSaveService(chFields, repos.Characters).RunAsync);
    tickTasks.Add(new BuffExpiryService(chFields, buffs, new ChannelPackets(serverOps, config)).RunAsync);
    tickTasks.Add(new AirshipService(chFields, new ChannelPackets(serverOps, config)).RunAsync);
}

using CancellationTokenSource cts = ServerBootstrap.ShutdownToken();

Console.WriteLine($"Cronus Channel — JMS v{config.Version}, region {config.Region}");
Console.WriteLine($"  channels: {channelCount} — ports {channelPort}..{channelPort + channelCount - 1}, advertised to clients as {channelHost}");
if (channelHost.Equals(IPAddress.Loopback))
{
    Console.WriteLine("  (localhost only — set CRONUS_HOST=<your LAN/public IP> so friends can connect)");
}

Console.WriteLine("Press Ctrl+C to stop.");

try
{
    var tasks = new List<Task>();
    tasks.AddRange(channelListeners.Select(l => l.RunAsync(cts.Token)));
    if (cashShopListener is not null)
    {
        tasks.Add(cashShopListener.RunAsync(cts.Token));
    }

    tasks.AddRange(tickTasks.Select(run => run(cts.Token)));
    tasks.Add(HeartbeatLoopAsync(cts.Token));
    tasks.Add(world.SubscribeAsync(instanceId, OnWorldEventAsync, cts.Token));
    await Task.WhenAll(tasks);
}
finally
{
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

// ---- World plumbing ---------------------------------------------------------------------------

// Every few seconds: tell the World we are alive; if it restarted, register again.
async Task HeartbeatLoopAsync(CancellationToken ct)
{
    using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(1, registration.HeartbeatSeconds)));
    try
    {
        while (await timer.WaitForNextTickAsync(ct))
        {
            bool? known = await world.HeartbeatAsync(instanceId, ct);
            if (known != false)
            {
                continue;
            }

            Console.WriteLine("[world] the World restarted — registering the channels again");
            RegisterChannelsResponse? again = await world.RegisterChannelsAsync(instanceId, channelHost, channelPorts, cashShopEnabled ? cashShopPort : null, attempts: 1, cancellationToken: ct);
            if (again is null)
            {
                continue;
            }

            if (!again.ChannelIds.SequenceEqual(channelIds))
            {
                Console.WriteLine($"[world] warning: the World now numbers these channels {string.Join(", ", again.ChannelIds.Select(id => id + 1))} (they were {string.Join(", ", channelIds.Select(id => id + 1))}); restart this process to renumber");
            }

            ApplyTimetable(again.Timetable);
        }
    }
    catch (OperationCanceledException)
    {
        // shutdown
    }
}

// The World's callbacks.
async ValueTask OnWorldEventAsync(WorldEvent ev)
{
    switch (ev.EventCase)
    {
        case WorldEvent.EventOneofCase.Broadcast:
        {
            byte[] packet = ev.Broadcast.Packet.ToByteArray();
            for (int i = 0; i < channelFields.Count; i++)
            {
                if (channelIds[i] == ev.Broadcast.ExcludeChannel)
                {
                    continue;
                }

                foreach (Field field in channelFields[i].Fields)
                {
                    await field.BroadcastAsync(packet);
                }
            }

            break;
        }

        case WorldEvent.EventOneofCase.Disconnect:
        {
            foreach (FieldRegistry fields in channelFields)
            {
                foreach (Field field in fields.Fields)
                {
                    foreach (FieldPlayer player in field.Players.ToArray())
                    {
                        if (player.Character.Id == ev.Disconnect.CharacterId)
                        {
                            Console.WriteLine($"[world] disconnecting {player.Character.Name}: logged in elsewhere");
                            await player.Session.DisposeAsync();
                        }
                    }
                }
            }

            break;
        }

        case WorldEvent.EventOneofCase.Timetable:
            ApplyTimetable(ev.Timetable);
            break;
    }
}

// The airships follow the World's timetable (pure wall-clock arithmetic, so every channel agrees).
static void ApplyTimetable(Timetable t)
{
    AirshipTimetable timetable = ProtoConvert.ToTimetable(t);
    if (AirshipSchedule.Apply(timetable.Cycle, timetable.Boarding, timetable.RaidStart, timetable.RaidEndBeforeArrival))
    {
        GameConstants.AirshipBalrogChance = timetable.BalrogChance;
        Console.WriteLine($"[airship] timetable from the World: {timetable.Cycle.TotalMinutes:0.#}-minute cycle, boarding {timetable.Boarding.TotalMinutes:0.#} min, Balrog raid {timetable.BalrogChance:P0} of flights");
    }
    else
    {
        Console.WriteLine($"[airship] the World's timetable is unusable (cycle {timetable.Cycle}, boarding {timetable.Boarding}); keeping the current one");
    }
}

// ---- Game data and content providers, all from environment variables ------------------------

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

// Mob drop tables from the reference drop_data.sql dump if CRONUS_DROPS points at one, else no
// tables (mobs fall back to a small placeholder meso pile).
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

// NPC shops from a shops+shopitems SQL dump if CRONUS_SHOPS points at one, else no shops.
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
