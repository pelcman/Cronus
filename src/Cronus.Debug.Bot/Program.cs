using System.Net;
using Cronus.Common;
using Cronus.Debug.Bot;
using Cronus.Network.Packets;

// Cronus.Debug.Bot — a protocol-level content debugger. It launches N real encrypted client
// sessions (default 4) against a running server, walks each through the content checklist
// (login, entry, commands, NPC dialogs, salon UI, whisper, channel change, cash shop, boss
// doors), pairs bots up for whisper/party, and reports OK/NG per step.
//
//   dotnet run --project src/Cronus.Debug.Bot            # 4 bots vs 127.0.0.1:8484
//   dotnet run --project src/Cronus.Debug.Bot -- 8       # 8 bots
//   CRONUS_BOT_COUNT / CRONUS_BOT_HOST / CRONUS_BOT_PORT override the defaults.

DotEnv.Load();                 // the repo-root .env feeds CRONUS_BOT_* like the server's vars
CodePage.Register();
ServerConfig config = ServerConfig.Jms186;

int botCount = args.Length > 0 && int.TryParse(args[0], out int argCount)
    ? argCount
    : int.TryParse(Environment.GetEnvironmentVariable("CRONUS_BOT_COUNT"), out int envCount) ? envCount : 4;
botCount = Math.Clamp(botCount, 1, 32);

string host = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("CRONUS_BOT_HOST") ?? "127.0.0.1";
int loginPort = int.TryParse(Environment.GetEnvironmentVariable("CRONUS_BOT_PORT"), out int p) ? p : 8484;

string opcodeDir = Path.Combine(AppContext.BaseDirectory, "opcodes");
OpcodeTable clientOps = OpcodeTable.LoadFile(Path.Combine(opcodeDir, "JMS_v186_ClientPacket.properties"));
OpcodeTable serverOps = OpcodeTable.LoadFile(Path.Combine(opcodeDir, "JMS_v186_ServerPacket.properties"));

var loginEndpoint = new IPEndPoint(IPAddress.Parse(host), loginPort);
Console.WriteLine($"Cronus.Debug.Bot — {botCount} bots vs {loginEndpoint}");

// Real client windows run alongside the bots (default on; CRONUS_BOT_LAUNCH=0 disables).
bool launchClients = Environment.GetEnvironmentVariable("CRONUS_BOT_LAUNCH")?.Trim() is not ("0" or "false" or "off");
System.Collections.Generic.IReadOnlyList<System.Diagnostics.Process> clientWindows = Array.Empty<System.Diagnostics.Process>();
if (launchClients)
{
    string? clientPath = ClientLauncher.ResolveClientPath();
    if (clientPath is null)
    {
        Console.WriteLine("[launcher] no client found (set CRONUS_CLIENT_PATH) — running bots only.");
    }
    else
    {
        Console.WriteLine($"[launcher] launching {botCount} client window(s) from {clientPath}");
        clientWindows = ClientLauncher.Launch(botCount, clientPath);
    }
}

var results = new List<StepResult>();
var scenarios = new Scenarios(loginEndpoint, results);
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(20));

// Drop-sweep audit mode: CRONUS_BOT_AUDIT_DROPS=<file of item ids> logs one bot in and spawns
// every listed item as a ground drop, validating each DropEnterField the client would render.
string? sweepFile = Environment.GetEnvironmentVariable("CRONUS_BOT_AUDIT_DROPS");
if (!string.IsNullOrWhiteSpace(sweepFile) && File.Exists(sweepFile))
{
    var ids = File.ReadAllLines(sweepFile)
        .Select(l => int.TryParse(l.Trim().TrimEnd(','), out int v) ? v : -1)
        .Where(v => v > 0)
        .Distinct()
        .ToList();
    Console.WriteLine($"Cronus.Debug.Bot — drop-sweep of {ids.Count} item ids vs {loginEndpoint}");

    var sweeper = new BotClient("Sweeper", config, clientOps, serverOps) { CharacterName = "CronusSweep" };
    await scenarios.LoginAndEnterAsync(sweeper, 1, cts.Token);
    await sweeper.ExpectAsync(ServerOpcode.SetField).ConfigureAwait(false);
    List<string> bad = await scenarios.SweepDropsAsync(sweeper, ids);
    await sweeper.DisposeAsync();

    Console.WriteLine();
    Console.WriteLine($"==== drop-sweep: {ids.Count - bad.Count}/{ids.Count} clean ====");
    if (bad.Count > 0)
    {
        Console.WriteLine("malformed / missing: " + string.Join(", ", bad.Take(60)));
    }

    return bad.Count == 0 ? 0 : 1;
}

var bots = new List<BotClient>();
for (int i = 1; i <= botCount; i++)
{
    bots.Add(new BotClient($"Bot{i}", config, clientOps, serverOps) { CharacterName = $"CronusBot{i}" });
}

// Phase 1: everyone logs in and enters the game, then walks the solo content suite.
await Task.WhenAll(bots.Select((bot, i) => Task.Run(async () =>
{
    await scenarios.LoginAndEnterAsync(bot, i + 1, cts.Token);
    if (bot.CharacterId != 0)
    {
        // Only the first bot runs the field-mutating drop-pickup audit; ground drops broadcast to
        // the whole field, so concurrent droppers would cross each other's streams (false NGs).
        await scenarios.RunSoloSuiteAsync(bot, cts.Token, fieldAudits: i == 0);
    }
}, cts.Token)));

// Phase 2: paired content — whisper across bots (they may sit on different channels after the
// channel-change step) and a party invite/accept between the first pair.
if (bots.Count >= 2 && bots[0].CharacterId != 0 && bots[1].CharacterId != 0)
{
    await scenarios.RunWhisperPairAsync(bots[0], bots[1]);
    await scenarios.RunPartyPairAsync(bots[0], bots[1]);
}

foreach (BotClient bot in bots)
{
    await bot.DisposeAsync();
}

// ---- report ------------------------------------------------------------------------------
int ok = results.Count(r => r.Ok);
int ng = results.Count - ok;
Console.WriteLine();
Console.WriteLine($"==== Cronus.Debug.Bot report: {ok} OK / {ng} NG ====");
foreach (IGrouping<string, StepResult> byBot in results.GroupBy(r => r.Bot))
{
    Console.WriteLine($"  {byBot.Key}: {byBot.Count(r => r.Ok)}/{byBot.Count()} OK");
    foreach (StepResult r in byBot.Where(r => !r.Ok))
    {
        Console.WriteLine($"    NG {r.Step} — {r.Detail}");
    }
}

return ng == 0 ? 0 : 1;
