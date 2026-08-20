using System.Net;
using Cronus.Common;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Host;
using Cronus.Server.Login;

// JMS v186 login server. Point a JMS v186 client (via EmuClient localhost redirect) at this
// host's login port to reach the ID/PW screen.

CodePage.Register();
ServerConfig config = ServerConfig.Jms186;

int port = args.Length > 0 && int.TryParse(args[0], out int p) ? p : 8484;

string opcodeDir = Path.Combine(AppContext.BaseDirectory, "opcodes");
OpcodeTable clientOps = OpcodeTable.LoadFile(Path.Combine(opcodeDir, "JMS_v186_ClientPacket.properties"));
OpcodeTable serverOps = OpcodeTable.LoadFile(Path.Combine(opcodeDir, "JMS_v186_ServerPacket.properties"));

WarnUnresolved("client", clientOps);
WarnUnresolved("server", serverOps);

var accounts = new InMemoryAccountRepository();
var loginService = new LoginService(accounts, autoRegister: true);

var listener = new MapleListener(
    new IPEndPoint(IPAddress.Any, port),
    config,
    () => new LoggingHandler(
        new LoginHandler(clientOps, serverOps, loginService, config),
        "login"));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

Console.WriteLine($"Cronus login server — JMS v{config.Version}, region {config.Region} — listening on 0.0.0.0:{port}");
Console.WriteLine("Accounts auto-register on first login. Press Ctrl+C to stop.");

try
{
    await listener.RunAsync(cts.Token);
}
finally
{
    await listener.DisposeAsync();
}

Console.WriteLine("Stopped.");

static void WarnUnresolved(string which, OpcodeTable table)
{
    if (table.UnresolvedNames.Count > 0)
    {
        Console.WriteLine($"[warn] {which} opcodes unresolved: {string.Join(", ", table.UnresolvedNames)}");
    }
}
