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
(IAccountRepository accounts, ICharacterRepository characters) = CreateRepositories();
var loginService = new LoginService(accounts, autoRegister: true);

// The login server hands clients to the channel via LP_SelectCharacterResult.
var channelEndpoint = new IPEndPoint(IPAddress.Loopback, channelPort);

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

// NPC scripts from CRONUS_SCRIPTS/npc/{id}.js, if configured.
NpcScriptEngine? npcScripts = CreateNpcScriptEngine();

var channelListener = new MapleListener(
    new IPEndPoint(IPAddress.Any, channelPort),
    config,
    () => new LoggingHandler(
        new ChannelHandler(clientOps, serverOps, characters, config, fields, maps, npcScripts, channelId: 0),
        "channel"),
    keepAlive);

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Cronus — JMS v{config.Version}, region {config.Region}");
Console.WriteLine($"  login   : 0.0.0.0:{loginPort}");
Console.WriteLine($"  channel : 0.0.0.0:{channelPort}");
Console.WriteLine("Accounts auto-register on first login. Press Ctrl+C to stop.");

try
{
    await Task.WhenAll(
        loginListener.RunAsync(cts.Token),
        channelListener.RunAsync(cts.Token));
}
finally
{
    await loginListener.DisposeAsync();
    await channelListener.DisposeAsync();
}

Console.WriteLine("Stopped.");

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

static NpcScriptEngine? CreateNpcScriptEngine()
{
    string? scriptRoot = Environment.GetEnvironmentVariable("CRONUS_SCRIPTS");
    if (string.IsNullOrWhiteSpace(scriptRoot))
    {
        Console.WriteLine("[npc] CRONUS_SCRIPTS not set — NPC dialogs disabled.");
        return null;
    }

    string npcDir = Path.Combine(scriptRoot, "npc");
    Console.WriteLine($"[npc] Loading NPC scripts on demand from {npcDir}");
    return new NpcScriptEngine(new FolderNpcScriptSource(npcDir));
}

static void WarnUnresolved(string which, OpcodeTable table)
{
    if (table.UnresolvedNames.Count > 0)
    {
        Console.WriteLine($"[warn] {which} opcodes unresolved: {string.Join(", ", table.UnresolvedNames)}");
    }
}

static (IAccountRepository, ICharacterRepository) CreateRepositories()
{
    string? connectionString = Environment.GetEnvironmentVariable("CRONUS_DB");
    if (string.IsNullOrWhiteSpace(connectionString))
    {
        Console.WriteLine("[db] CRONUS_DB not set — using in-memory stores (not persistent).");
        return (new InMemoryAccountRepository(), new InMemoryCharacterRepository());
    }

    try
    {
        Func<CronusDbContext> factory = MySqlDatabase.CreateFactory(connectionString);
        MySqlDatabase.EnsureCreated(factory);
        Console.WriteLine("[db] Connected to MySQL; accounts and characters are persistent.");
        return (new DbAccountRepository(factory), new DbCharacterRepository(factory));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[db] MySQL unavailable ({ex.Message}); falling back to in-memory stores.");
        return (new InMemoryAccountRepository(), new InMemoryCharacterRepository());
    }
}
