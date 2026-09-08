using System.Net;
using Cronus.Server.Core;
using Microsoft.AspNetCore.Server.Kestrel.Core;

// The World process (Maple2's Maple2.Server.World): one per world. The Login process asks it for
// the channel list, the Channel processes register their channels with it and heartbeat, every
// hand-off between processes (login → channel, channel → channel, channel ↔ cash shop) goes
// through it, and it fans world-wide broadcasts out to every Channel process. It talks gRPC on
// the loopback interface only unless CRONUS_WORLD_BIND says otherwise; clients never reach it.
//
// run-server.bat starts it first; alone: dotnet run --project src/Cronus.Server.World

ServerBootstrap boot = ServerBootstrap.Start("world");

IPAddress bind = ServerBootstrap.ResolveHost(ServerBootstrap.Str("CRONUS_WORLD_BIND"), IPAddress.Loopback);
int port = ServerBootstrap.Int("CRONUS_WORLD_PORT", GrpcWorldClient.DefaultPort);
string worldName = ServerBootstrap.Str("CRONUS_WORLD_NAME") ?? "Cronus";

// The airship timetable is the World's to set (every channel of every process follows it):
// CRONUS_AIRSHIP_CYCLE_MIN / CRONUS_AIRSHIP_BOARDING_MIN / CRONUS_AIRSHIP_BALROG_CHANCE.
AirshipTimetable timetable = AirshipTimetable.Default with
{
    Cycle = TimeSpan.FromMinutes(ServerBootstrap.Double("CRONUS_AIRSHIP_CYCLE_MIN", AirshipTimetable.Default.Cycle.TotalMinutes)),
    Boarding = TimeSpan.FromMinutes(ServerBootstrap.Double("CRONUS_AIRSHIP_BOARDING_MIN", AirshipTimetable.Default.Boarding.TotalMinutes)),
    BalrogChance = Math.Clamp(ServerBootstrap.Double("CRONUS_AIRSHIP_BALROG_CHANCE", AirshipTimetable.Default.BalrogChance), 0.0, 1.0),
};
if (timetable.Cycle <= TimeSpan.Zero || timetable.Boarding <= TimeSpan.Zero || timetable.Boarding >= timetable.Cycle)
{
    Console.WriteLine($"[airship] timetable rejected (cycle {timetable.Cycle}, boarding {timetable.Boarding}) — boarding must be shorter than the cycle; using the default");
    timetable = AirshipTimetable.Default;
}

var state = new WorldState(worldName, timetable);
var service = new WorldGrpcService(state);

WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.ConfigureKestrel(kestrel =>
{
    kestrel.Listen(bind, port, listen => listen.Protocols = HttpProtocols.Http2);
});
builder.Services.AddGrpc();
builder.Services.AddSingleton(service);

WebApplication app = builder.Build();
app.MapGrpcService<WorldGrpcService>();

Console.WriteLine($"Cronus World — {worldName} (JMS v{boot.Config.Version})");
Console.WriteLine($"  grpc    : listening on {bind}:{port} — the Login and Channel processes connect here (CRONUS_WORLD_URI)");
Console.WriteLine($"  airships: {timetable.Cycle.TotalMinutes:0.#}-minute cycle, boarding {timetable.Boarding.TotalMinutes:0.#} min, Balrog raid {timetable.BalrogChance:P0} of flights");
Console.WriteLine("Waiting for Channel processes to register. Press Ctrl+C to stop.");

using CancellationTokenSource cts = ServerBootstrap.ShutdownToken();
try
{
    await Task.WhenAll(app.RunAsync(cts.Token), service.RunMaintenanceAsync(cts.Token));
}
catch (OperationCanceledException)
{
    // Ctrl+C
}

Console.WriteLine("Stopped.");
