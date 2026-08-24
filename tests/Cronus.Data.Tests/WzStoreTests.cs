using Cronus.Data;
using Cronus.Data.Wz;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Cronus.Data.Tests;

/// <summary>
/// The two wz stores must be interchangeable: identical reads, existence checks, and
/// enumeration over the same relative paths — that is what lets every provider run unchanged
/// on either a loose dump tree or the ingested gamedata.db.
/// </summary>
public class WzStoreTests
{
    private const string Doc = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><imgdir name="t.img"><int name="a" value="1"/></imgdir>""";

    [Fact]
    public void DirectoryStore_ReadsExistsAndEnumerates()
    {
        string root = Path.Combine(Path.GetTempPath(), "cronus-store-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(root, "Map", "Map1"));
        File.WriteAllText(Path.Combine(root, "Map", "Map1", "100000000.img.xml"), Doc);
        try
        {
            var store = new DirectoryWzStore(root);
            Assert.Equal(Doc, store.ReadText("Map/Map1/100000000.img.xml"));
            Assert.True(store.Exists("Map/Map1/100000000.img.xml"));
            Assert.False(store.Exists("Map/Map1/999999999.img.xml"));
            Assert.Null(store.ReadText("Map/Map1/999999999.img.xml"));
            Assert.Equal(new[] { "Map/Map1/100000000.img.xml" }, store.EnumeratePaths("Map"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SqliteStore_RoundTripsWhatTheIngestWrites()
    {
        string db = Path.Combine(Path.GetTempPath(), "cronus-store-" + Guid.NewGuid() + ".db");
        try
        {
            // Write one row the way WzIngest does (deflate blob + meta).
            using (var conn = new SqliteConnection($"Data Source={db}"))
            {
                conn.Open();
                using var create = new SqliteCommand("""
                    CREATE TABLE wz_img (path TEXT PRIMARY KEY, xml BLOB NOT NULL);
                    CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
                    INSERT INTO meta VALUES ('source', 'test');
                    """, conn);
                create.ExecuteNonQuery();

                byte[] raw = System.Text.Encoding.UTF8.GetBytes(Doc);
                using var buffer = new MemoryStream();
                using (var deflate = new System.IO.Compression.DeflateStream(
                    buffer, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
                {
                    deflate.Write(raw);
                }

                using var insert = new SqliteCommand("INSERT INTO wz_img VALUES ($p, $x)", conn);
                insert.Parameters.AddWithValue("$p", "Map/Map1/100000000.img.xml");
                insert.Parameters.AddWithValue("$x", buffer.ToArray());
                insert.ExecuteNonQuery();
            }

            SqliteConnection.ClearAllPools();
            var store = new SqliteWzStore(db);
            Assert.Equal(Doc, store.ReadText("Map/Map1/100000000.img.xml"));
            Assert.True(store.Exists("Map/Map1/100000000.img.xml"));
            Assert.False(store.Exists("nope/x.img.xml"));
            Assert.Null(store.ReadText("nope/x.img.xml"));
            Assert.Equal(new[] { "Map/Map1/100000000.img.xml" }, store.EnumeratePaths("Map"));
            Assert.Equal("test", store.Meta["source"]);

            // Backslash inputs normalize (providers sometimes build paths with Path.Combine).
            Assert.True(store.Exists(@"Map\Map1\100000000.img.xml"));
            SqliteConnection.ClearAllPools();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(db);
        }
    }

    [Fact]
    public void Providers_WorkOverTheSqliteStore()
    {
        // A provider running on the DB store must behave exactly as over loose files:
        // WzMapProvider is the representative (path building + parse).
        string db = Path.Combine(Path.GetTempPath(), "cronus-store-" + Guid.NewGuid() + ".db");
        string root = Path.Combine(Path.GetTempPath(), "cronus-store-src-" + Guid.NewGuid());
        Directory.CreateDirectory(root);
        try
        {
            const string mapDoc = """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><imgdir name="100000000.img"><imgdir name="info"><int name="returnMap" value="100000000"/></imgdir></imgdir>""";
            // Build a tiny db via the ingest's own writer path (private) — emulate with SQL.
            using (var conn = new SqliteConnection($"Data Source={db}"))
            {
                conn.Open();
                using var create = new SqliteCommand(
                    "CREATE TABLE wz_img (path TEXT PRIMARY KEY, xml BLOB NOT NULL);" +
                    "CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);", conn);
                create.ExecuteNonQuery();
                byte[] raw = System.Text.Encoding.UTF8.GetBytes(mapDoc);
                using var buffer = new MemoryStream();
                using (var deflate = new System.IO.Compression.DeflateStream(
                    buffer, System.IO.Compression.CompressionLevel.Fastest, leaveOpen: true))
                {
                    deflate.Write(raw);
                }

                using var insert = new SqliteCommand("INSERT INTO wz_img VALUES ('Map/Map1/100000000.img.xml', $x)", conn);
                insert.Parameters.AddWithValue("$x", buffer.ToArray());
                insert.ExecuteNonQuery();
            }

            SqliteConnection.ClearAllPools();
            var provider = new WzMapProvider(new SqliteWzStore(db));
            Assert.NotNull(provider.GetMap(100000000));
            Assert.Null(provider.GetMap(999999999));
            SqliteConnection.ClearAllPools();
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(db);
            Directory.Delete(root, recursive: true);
        }
    }
}
