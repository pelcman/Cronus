using Microsoft.EntityFrameworkCore;

namespace Cronus.Database;

/// <summary>
/// Builds a MySQL-backed <see cref="CronusDbContext"/> factory (via Pomelo). Detecting the
/// server version opens a connection, so call this only when a MySQL server is available;
/// callers that want a graceful fallback should wrap it in a try/catch.
/// </summary>
public static class MySqlDatabase
{
    public static Func<CronusDbContext> CreateFactory(string connectionString)
    {
        DbContextOptions<CronusDbContext> options =
            new DbContextOptionsBuilder<CronusDbContext>()
                .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                .Options;

        return () => new CronusDbContext(options);
    }

    /// <summary>Creates the schema if it does not exist. Interim; replace with migrations later.</summary>
    public static void EnsureCreated(Func<CronusDbContext> factory)
    {
        using CronusDbContext db = factory();
        db.Database.EnsureCreated();
    }
}
