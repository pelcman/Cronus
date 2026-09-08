using System.Net;
using Cronus.Common;
using Cronus.Network;
using Cronus.Server.Core;
using Cronus.Server.Login;

// The Login process (Maple2's Maple2.Server.Login): authentication, the world/channel list (from
// the World), character list / create / delete, and character select — which asks the World to
// record the hand-off and tells the client which Channel process to connect to.
//
// run-server.bat starts World → Login → Channel; alone: dotnet run --project src/Cronus.Server.Login [port]

ServerBootstrap boot = ServerBootstrap.Start("login");
ServerConfig config = boot.Config;

int loginPort = args.Length > 0 && int.TryParse(args[0], out int argPort) ? argPort : ServerBootstrap.Int("CRONUS_LOGIN_PORT", 8484);

Repositories repos = boot.CreateRepositories();

// Accounts auto-register on first login unless CRONUS_AUTO_REGISTER disables it (0/false/off).
bool autoRegister = ServerBootstrap.Flag("CRONUS_AUTO_REGISTER", true);
Console.WriteLine(autoRegister
    ? "[login] auto-register ON — unknown accounts are created on first login."
    : "[login] auto-register OFF — only existing accounts can log in.");
var loginService = new LoginService(repos.Accounts, autoRegister: autoRegister);

int startMap = ServerBootstrap.Int("CRONUS_STARTMAP", 100000000);
Console.WriteLine($"[map] new characters start in map {startMap}");

// The World: it must be up before the Login opens its port (Maple2's Login does the same).
await using var world = new GrpcWorldClient(boot.WorldAddress);
if (!await world.WaitUntilReachableAsync(attempts: 3, delay: TimeSpan.FromSeconds(5)))
{
    Console.WriteLine($"[world] World ({boot.WorldAddress}) に接続できません。run-server.bat は World → Login → Channel の順に起動します。World を先に起動するか、CRONUS_WORLD_URI を確認してください。");
    Environment.Exit(3);
    return;
}

WorldView view = await world.GetWorldAsync();
Console.WriteLine(view.Channels.Count == 0
    ? $"[world] connected to {world.Address} — no channels yet; they appear as Channel processes register."
    : $"[world] connected to {world.Address} — {view.Channels.Count} channel(s): {string.Join(", ", view.Channels.Select(c => $"{c.Name} {c.Endpoint}"))}");

var listener = new MapleListener(
    new IPEndPoint(IPAddress.Any, loginPort),
    config,
    () => new LoggingHandler(
        new LoginHandler(boot.ClientOps, boot.ServerOps, loginService, config,
            characters: repos.Characters, startMapId: startMap, world: world),
        "login", verbose: boot.WireDebug),
    keepAlive: null); // keep-alive disabled during login diagnosis

Console.WriteLine($"Cronus Login — JMS v{config.Version}, region {config.Region}");
Console.WriteLine($"  login : listening on 0.0.0.0:{loginPort}");
Console.WriteLine("Point the client at this port. Press Ctrl+C to stop.");

using CancellationTokenSource cts = ServerBootstrap.ShutdownToken();
try
{
    await listener.RunAsync(cts.Token);
}
finally
{
    await listener.DisposeAsync();
}

Console.WriteLine("Stopped.");
