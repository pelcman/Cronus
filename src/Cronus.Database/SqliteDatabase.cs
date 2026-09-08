using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Cronus.Database;

/// <summary>
/// Builds a SQLite-file-backed <see cref="CronusDbContext"/> factory. Since 2026-09-08 MySQL
/// (<see cref="MySqlDatabase"/>) is the standard store; SQLite stays as the explicit opt-in
/// (`CRONUS_DB=sqlite`) for a server without MySQL, as the test store, and as the source of the
/// one-time legacy import (<see cref="DatabaseCopy"/>).
/// </summary>
public static class SqliteDatabase
{
    /// <summary>Closes pooled connections so the file can be renamed or deleted (Windows keeps it locked otherwise).</summary>
    public static void ReleaseFiles() => Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

    public static Func<CronusDbContext> CreateFactory(string filePath)
    {
        DbContextOptions<CronusDbContext> options =
            new DbContextOptionsBuilder<CronusDbContext>()
                .UseSqlite($"Data Source={filePath}")
                .Options;

        return () => new CronusDbContext(options);
    }

    /// <summary>
    /// Creates the schema if missing, then applies the same additive migration policy as
    /// <see cref="MySqlDatabase.EnsureCreated"/>: new tables and new columns are added in place so
    /// a server upgrade never requires deleting the database file.
    /// </summary>
    public static void EnsureCreated(Func<CronusDbContext> factory)
    {
        using CronusDbContext db = factory();
        bool created = db.Database.EnsureCreated();
        if (!created)
        {
            SyncMissingTablesAndColumns(db);
        }
    }

    private static void SyncMissingTablesAndColumns(CronusDbContext db)
    {
        DbConnection connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            connection.Open();
        }

        foreach (IEntityType entity in db.Model.GetEntityTypes())
        {
            string? table = entity.GetTableName();
            if (table is null)
            {
                continue;
            }

            HashSet<string> existing = ExistingColumns(connection, table);
            if (existing.Count == 0)
            {
                // The whole table is new (added since the database file was created).
                string createSql = db.Database.GenerateCreateScript();
                foreach (string statement in createSql.Split(';'))
                {
                    if (statement.Contains($"CREATE TABLE \"{table}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        Execute(connection, statement);
                        Console.WriteLine($"[db] migrated: created table \"{table}\"");
                        break;
                    }
                }

                continue;
            }

            var storeObject = StoreObjectIdentifier.Table(table, entity.GetSchema());
            foreach (IProperty property in entity.GetProperties())
            {
                string? column = property.GetColumnName(storeObject);
                if (column is null || existing.Contains(column))
                {
                    continue;
                }

                // SQLite allows DEFAULTs on any column type, so one additive form covers all.
                string type = property.GetColumnType(storeObject);
                string defaultLiteral = property.ClrType == typeof(string) || type.Contains("TEXT", StringComparison.OrdinalIgnoreCase)
                    ? $"'{TextDefault(property)}'"
                    : "0";
                string nullability = property.IsNullable ? "NULL" : $"NOT NULL DEFAULT {defaultLiteral}";
                Execute(connection, $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {type} {nullability}");
                Console.WriteLine($"[db] migrated: added \"{table}\".\"{column}\" ({type})");
            }
        }
    }

    /// <summary>A backfill value a text column's converter can read back (JSON maps/lists or "").</summary>
    private static string TextDefault(IProperty property)
    {
        Type t = property.ClrType;
        if (t.IsGenericType)
        {
            Type def = t.GetGenericTypeDefinition();
            if (def == typeof(Dictionary<,>))
            {
                return "{}";
            }

            if (def == typeof(List<>))
            {
                return "[]";
            }
        }

        return string.Empty;
    }

    private static HashSet<string> ExistingColumns(DbConnection connection, string table)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using DbCommand cmd = connection.CreateCommand();
        // PRAGMA doesn't support parameters; the table name comes from the EF model, not user input.
        cmd.CommandText = $"PRAGMA table_info(\"{table}\")";
        using DbDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1)); // column 1 = name
        }

        return columns;
    }

    private static void Execute(DbConnection connection, string sql)
    {
        using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
