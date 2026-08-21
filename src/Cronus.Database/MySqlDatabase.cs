using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

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

    /// <summary>
    /// Creates the schema if it does not exist, then applies an additive migration: any table or
    /// column the model has that the live database lacks is created/added in place, so upgrading
    /// the server never requires dropping an existing database. (Removed/retyped columns are left
    /// alone — additive only.)
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
                // The whole table is new (added since the database was created).
                string createSql = db.Database.GenerateCreateScript();
                foreach (string statement in createSql.Split(';'))
                {
                    if (statement.Contains($"CREATE TABLE `{table}`", StringComparison.OrdinalIgnoreCase))
                    {
                        Execute(connection, statement);
                        Console.WriteLine($"[db] migrated: created table `{table}`");
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

                string type = property.GetColumnType(storeObject);
                bool isText = type.Contains("text", StringComparison.OrdinalIgnoreCase)
                    || type.Contains("blob", StringComparison.OrdinalIgnoreCase)
                    || type.Contains("json", StringComparison.OrdinalIgnoreCase);

                if (isText)
                {
                    // MySQL forbids plain DEFAULTs on text columns: add nullable, then backfill.
                    Execute(connection, $"ALTER TABLE `{table}` ADD COLUMN `{column}` {type} NULL");
                    Execute(connection, $"UPDATE `{table}` SET `{column}` = '{TextDefault(property)}' WHERE `{column}` IS NULL");
                }
                else
                {
                    string defaultLiteral = property.ClrType == typeof(string) ? "''" : "0";
                    string nullability = property.IsNullable ? "NULL" : $"NOT NULL DEFAULT {defaultLiteral}";
                    Execute(connection, $"ALTER TABLE `{table}` ADD COLUMN `{column}` {type} {nullability}");
                }

                Console.WriteLine($"[db] migrated: added `{table}`.`{column}` ({type})");
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
        cmd.CommandText =
            "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS " +
            "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @table";
        DbParameter p = cmd.CreateParameter();
        p.ParameterName = "@table";
        p.Value = table;
        cmd.Parameters.Add(p);
        using DbDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            columns.Add(reader.GetString(0));
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
