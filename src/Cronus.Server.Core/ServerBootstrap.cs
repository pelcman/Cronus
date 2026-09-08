using System.Net;
using Cronus.Common;
using Cronus.Database;
using Cronus.Domain;
using Cronus.Network;
using Cronus.Network.Packets;

namespace Cronus.Server.Core;

/// <summary>The persistent stores every process opens against the one MySQL database.</summary>
public sealed record Repositories(
    IAccountRepository Accounts,
    ICharacterRepository Characters,
    IStorageRepository? Storage,
    IKeymapRepository? Keymaps,
    IGuildRepository? Guilds,
    IHiredMerchantRepository? Merchants,
    IParcelRepository Parcels);

/// <summary>
/// The start-up every server process shares: the repo-root <c>.env</c>, the console→file log
/// mirror, the JMS code page, the opcode tables, wire diagnostics, the database. Each process's
/// <c>Program.cs</c> calls <see cref="Start"/> first and then wires what is specific to it.
/// </summary>
public sealed class ServerBootstrap
{
    private ServerBootstrap(string role, string? envFile, string? logFile, OpcodeTable clientOps, OpcodeTable serverOps, bool wireDebug)
    {
        Role = role;
        EnvFile = envFile;
        LogFile = logFile;
        ClientOps = clientOps;
        ServerOps = serverOps;
        WireDebug = wireDebug;
    }

    /// <summary>world / login / channel — names the log file and the console lines.</summary>
    public string Role { get; }

    public string? EnvFile { get; }

    public string? LogFile { get; }

    public ServerConfig Config { get; } = ServerConfig.Jms186;

    public OpcodeTable ClientOps { get; }

    public OpcodeTable ServerOps { get; }

    /// <summary>CRONUS_DEBUG: hex-dump every packet (heavy; off by default).</summary>
    public bool WireDebug { get; }

    /// <summary>The address clients are told to connect to (CRONUS_HOST), IPv4; loopback by default.</summary>
    public IPAddress AdvertisedHost => ResolveHost(Environment.GetEnvironmentVariable("CRONUS_HOST"), IPAddress.Loopback);

    /// <summary>Where the World process listens for the other processes.</summary>
    public Uri WorldAddress => GrpcWorldClient.ResolveAddress();

    /// <summary>Loads .env, attaches the log mirror, registers the code page, loads the opcode tables.</summary>
    public static ServerBootstrap Start(string role)
    {
        string? envFile = DotEnv.Load();
        string? logFile = TeeLog.Attach(role);
        Console.WriteLine(envFile is null
            ? "[env] no .env file found — using process environment only (see .env.example)."
            : $"[env] loaded {envFile}");
        if (logFile is not null)
        {
            Console.WriteLine($"[log] mirroring console to {logFile}");
        }

        CodePage.Register();

        string opcodeDir = Path.Combine(AppContext.BaseDirectory, "opcodes");
        OpcodeTable clientOps = OpcodeTable.LoadFile(Path.Combine(opcodeDir, "JMS_v186_ClientPacket.properties"));
        OpcodeTable serverOps = OpcodeTable.LoadFile(Path.Combine(opcodeDir, "JMS_v186_ServerPacket.properties"));
        WarnUnresolved("client", clientOps);
        WarnUnresolved("server", serverOps);

        bool wireDebug = Flag("CRONUS_DEBUG", false);
        if (wireDebug)
        {
            Console.WriteLine("[debug] wire diagnostics ON — every packet is logged (CRONUS_DEBUG).");
            MapleSession.DebugOnSend = (sessionRole, body) =>
            {
                ReadOnlySpan<byte> b = body.Span;
                int opcode = b.Length >= 2 ? b[0] | (b[1] << 8) : -1;
                Console.WriteLine($"[send:{sessionRole}] opcode 0x{opcode:X4} ({b.Length} bytes): {Convert.ToHexString(b)}");
            };
        }

        return new ServerBootstrap(role, envFile, logFile, clientOps, serverOps, wireDebug);
    }

    /// <summary>An environment switch: 1/true/on/yes → true, 0/false/off/no → false, unset → fallback.</summary>
    public static bool Flag(string name, bool fallback)
        => Environment.GetEnvironmentVariable(name)?.Trim().ToLowerInvariant() switch
        {
            "1" or "true" or "on" or "yes" => true,
            "0" or "false" or "off" or "no" => false,
            _ => fallback,
        };

    public static int Int(string name, int fallback)
        => int.TryParse(Environment.GetEnvironmentVariable(name), out int v) ? v : fallback;

    public static double Double(string name, double fallback)
        => double.TryParse(Environment.GetEnvironmentVariable(name), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : fallback;

    public static string? Str(string name)
    {
        string? v = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(v) ? null : v;
    }

    /// <summary>LP_AliveReq body: the server pings idle clients so they keep the connection open.</summary>
    public byte[] KeepAlivePacket()
        => new PacketWriter(ServerOps.Get(ServerOpcode.AliveReq), Config.PacketHeaderSize, Config.CodePage).ToArray();

    /// <summary>A token that is cancelled by Ctrl+C.</summary>
    public static CancellationTokenSource ShutdownToken()
    {
        var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };
        return cts;
    }

    /// <summary>
    /// Resolves an IPv4 literal or a hostname to the address advertised to clients (JMS sends it
    /// as 4 bytes). Empty or unresolvable → fallback.
    /// </summary>
    public static IPAddress ResolveHost(string? host, IPAddress fallback)
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
                    return addr;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[net] could not resolve '{host}' ({ex.Message}); using {fallback}");
        }

        return fallback;
    }

    /// <summary>
    /// Opens the stores. Default: MySQL (the standard since 2026-09-08; database Cronus186, shared
    /// by every process). <c>CRONUS_DB</c> may be a full connection string, <c>sqlite</c> (one
    /// file — fine for one process, not for three), or <c>memory</c> (per process, so the Login
    /// and the Channel would not see each other's accounts: for single-process tests only).
    /// MySQL unreachable → exit code 2, never a silent in-memory fallback.
    /// </summary>
    /// <param name="importLegacySqlite">Only the Channel process imports an old cronus.db, so two
    /// processes starting together cannot both do it.</param>
    public Repositories CreateRepositories(bool importLegacySqlite = false)
    {
        string? mode = Environment.GetEnvironmentVariable("CRONUS_DB");

        if (string.Equals(mode, "memory", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine("[db] CRONUS_DB=memory — in-memory stores, not persistent and private to this process (the Login and the Channel will not share accounts).");
            return Memory();
        }

        if (string.Equals(mode, "sqlite", StringComparison.OrdinalIgnoreCase))
        {
            string dbFile = LegacySqlitePath();
            try
            {
                Func<CronusDbContext> factory = SqliteDatabase.CreateFactory(dbFile);
                SqliteDatabase.EnsureCreated(factory);
                Console.WriteLine($"[db] SQLite at {dbFile} — one file shared by the processes; MySQL is the supported store.");
                return Db(factory);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[db] SQLite unavailable ({ex.Message}); falling back to in-memory stores.");
                return Memory();
            }
        }

        string connectionString = !string.IsNullOrWhiteSpace(mode)
            ? mode
            : MySqlDatabase.BuildConnectionString(
                Environment.GetEnvironmentVariable("CRONUS_DB_HOST") ?? "127.0.0.1",
                Int("CRONUS_DB_PORT", 3306),
                Environment.GetEnvironmentVariable("CRONUS_DB_NAME") ?? MySqlDatabase.DefaultDatabaseName,
                Environment.GetEnvironmentVariable("CRONUS_DB_USER") ?? "root",
                Environment.GetEnvironmentVariable("CRONUS_DB_PASSWORD") ?? "root");
        string where = MySqlDatabase.Describe(connectionString);
        try
        {
            Func<CronusDbContext> factory = MySqlDatabase.CreateFactory(connectionString);
            MySqlDatabase.EnsureCreated(factory);
            Console.WriteLine($"[db] MySQL {where} — accounts, characters, storage, keymaps, guilds, merchants and parcels are persistent.");
            if (importLegacySqlite)
            {
                ImportLegacySqlite(factory);
            }

            return Db(factory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[db] MySQL に接続できません ({where}): {ex.Message}");
            Console.WriteLine("[db] MySQL 8 を起動し、.env の CRONUS_DB_HOST / CRONUS_DB_PORT / CRONUS_DB_USER / CRONUS_DB_PASSWORD を確認してください。");
            Console.WriteLine("[db] MySQL 無しで動かすには CRONUS_DB=sqlite（ファイル保存）または CRONUS_DB=memory（保存しない・単一プロセスのみ）。");
            Environment.Exit(2);
            throw;
        }
    }

    private static Repositories Memory()
        => new(new InMemoryAccountRepository(), new InMemoryCharacterRepository(), null, null, null, null, new InMemoryParcelRepository());

    private static Repositories Db(Func<CronusDbContext> factory)
        => new(
            new DbAccountRepository(factory),
            new DbCharacterRepository(factory),
            new DbStorageRepository(factory),
            new DbKeymapRepository(factory),
            new DbGuildRepository(factory),
            new DbHiredMerchantRepository(factory),
            new DbParcelRepository(factory));

    private static string LegacySqlitePath()
        => Environment.GetEnvironmentVariable("CRONUS_DB_FILE") ?? Path.Combine(AppContext.BaseDirectory, "cronus.db");

    /// <summary>
    /// First start on MySQL: when the database is still empty and the old SQLite save is next to the
    /// executable, copy everything across once (keys intact) and rename the file so it is never
    /// imported twice. A failure leaves the file alone and the MySQL database empty.
    /// </summary>
    private static void ImportLegacySqlite(Func<CronusDbContext> target)
    {
        string path = LegacySqlitePath();
        if (!File.Exists(path))
        {
            return;
        }

        using (CronusDbContext db = target())
        {
            if (db.Accounts.Any())
            {
                return;
            }
        }

        try
        {
            Func<CronusDbContext> source = SqliteDatabase.CreateFactory(path);
            DatabaseCopy.Report report = DatabaseCopy.CopyAll(source, target);
            SqliteDatabase.ReleaseFiles();
            string moved = path + ".imported";
            File.Move(path, moved, overwrite: true);
            Console.WriteLine($"[db] imported the SQLite save into MySQL: {report}. The file is now {Path.GetFileName(moved)} (kept as a backup).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[db] SQLite import skipped ({ex.Message}); MySQL starts empty and {path} is untouched.");
        }
    }

    private static void WarnUnresolved(string which, OpcodeTable table)
    {
        if (table.UnresolvedNames.Count > 0)
        {
            Console.WriteLine($"[warn] {which} opcodes unresolved: {string.Join(", ", table.UnresolvedNames)}");
        }
    }
}
