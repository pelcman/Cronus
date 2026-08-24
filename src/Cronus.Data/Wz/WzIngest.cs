using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Cronus.Data.Wz;

/// <summary>
/// Builds the game-data database from a client's .wz archives (the Maple2.File.Ingest idea):
/// every .img is parsed and stored as deflate-compressed wz_xml in one SQLite file, keyed by
/// the same relative paths the loose-dump tree used — so <see cref="SqliteWzStore"/> is a
/// drop-in replacement for a directory dump, built from the exact data the client renders.
/// </summary>
public static class WzIngest
{
    /// <summary>The archives the server consumes. Sound/UI/Effect/Morph/TamingMob/Base carry
    /// nothing the game logic reads, so they are skipped.</summary>
    public static readonly string[] ArchiveNames =
    {
        "String", "Quest", "Map", "Mob", "Npc", "Item", "Character", "Skill", "Etc", "Reactor",
    };

    /// <summary>
    /// Ingests <paramref name="clientDir"/>'s .wz files into <paramref name="outPath"/>
    /// (replacing it). Returns the number of images stored.
    /// </summary>
    public static long BuildDatabase(string clientDir, string outPath, Action<string>? log = null)
    {
        log ??= _ => { };
        var watch = Stopwatch.StartNew();
        if (File.Exists(outPath))
        {
            File.Delete(outPath); // a rebuild replaces the whole database
        }

        using var db = new SqliteConnection($"Data Source={outPath}");
        db.Open();
        Exec(db, "PRAGMA journal_mode=OFF; PRAGMA synchronous=OFF;");
        Exec(db, """
            CREATE TABLE wz_img (path TEXT PRIMARY KEY, xml BLOB NOT NULL);
            CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
            """);

        long total = 0;
        foreach (string wzName in ArchiveNames)
        {
            string wzPath = Path.Combine(clientDir, wzName + ".wz");
            if (!File.Exists(wzPath))
            {
                log($"[ingest] {wzName}.wz not found — skipped");
                continue;
            }

            using WzArchive archive = WzArchive.Open(wzPath);
            using SqliteTransaction tx = db.BeginTransaction();
            using var insert = new SqliteCommand("INSERT OR REPLACE INTO wz_img(path, xml) VALUES ($p, $x)", db, tx);
            SqliteParameter pPath = insert.Parameters.Add("$p", SqliteType.Text);
            SqliteParameter pXml = insert.Parameters.Add("$x", SqliteType.Blob);

            int count = 0;
            foreach (WzImageEntry image in archive.Images)
            {
                string xml;
                try
                {
                    xml = WzImageDumper.DumpXml(archive, image);
                }
                catch (InvalidDataException e)
                {
                    log($"[ingest] warn {wzName}/{image.RelativePath}: {e.Message}");
                    continue;
                }

                pPath.Value = StorePath(archive.BaseName, image);
                pXml.Value = Deflate(xml);
                insert.ExecuteNonQuery();
                count++;
            }

            tx.Commit();
            total += count;
            log($"[ingest] {wzName}.wz — v{archive.Version}, iv={archive.IvName}, {count} images");
        }

        using (var meta = new SqliteCommand(
            "INSERT INTO meta VALUES ('source', $s), ('images', $i), ('ingested_utc', $t)", db))
        {
            meta.Parameters.AddWithValue("$s", clientDir);
            meta.Parameters.AddWithValue("$i", total.ToString());
            meta.Parameters.AddWithValue("$t", DateTime.UtcNow.ToString("O"));
            meta.ExecuteNonQuery();
        }

        Exec(db, "VACUUM;");
        log($"[ingest] done: {total} images in {watch.Elapsed.TotalSeconds:0.0}s -> {outPath}");
        return total;
    }

    /// <summary>
    /// The dump-style relative path. One structural quirk to mirror: an archive whose root holds
    /// a directory named like itself (Map.wz/Map/Map0/…) collapses that level, matching the
    /// wz_xml layout the providers were written against (Map/Map0/….img.xml).
    /// </summary>
    public static string StorePath(string baseName, WzImageEntry image)
    {
        string dir = image.Directory;
        if (dir == baseName)
        {
            dir = "";
        }
        else if (dir.StartsWith(baseName + "/", StringComparison.Ordinal))
        {
            dir = dir[(baseName.Length + 1)..];
        }

        return dir.Length == 0 ? $"{baseName}/{image.Name}.xml" : $"{baseName}/{dir}/{image.Name}.xml";
    }

    private static byte[] Deflate(string xml)
    {
        byte[] raw = Encoding.UTF8.GetBytes(xml);
        using var buffer = new MemoryStream();
        using (var deflate = new DeflateStream(buffer, CompressionLevel.Fastest, leaveOpen: true))
        {
            deflate.Write(raw);
        }

        return buffer.ToArray();
    }

    private static void Exec(SqliteConnection db, string sql)
    {
        using var cmd = new SqliteCommand(sql, db);
        cmd.ExecuteNonQuery();
    }
}
