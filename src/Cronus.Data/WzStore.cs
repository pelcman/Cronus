using System.IO.Compression;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Cronus.Data;

/// <summary>
/// Where wz_xml documents come from. Two backings: a directory of loose <c>.img.xml</c> files
/// (the classic dump tree) and the single-file game-data database that <c>Cronus.Ingest</c>
/// builds straight from a client's .wz archives. Providers only ever deal in relative paths
/// with forward slashes ("Map/Map1/100000000.img.xml").
/// </summary>
public interface IWzStore
{
    /// <summary>The document at <paramref name="relativePath"/>, or null when absent.</summary>
    string? ReadText(string relativePath);

    bool Exists(string relativePath);

    /// <summary>Every stored path that starts with <paramref name="prefix"/> (a directory-style
    /// prefix such as "Character/"; pass "" for everything).</summary>
    IEnumerable<string> EnumeratePaths(string prefix);
}

/// <summary>A wz_xml tree on disk (e.g. DevTools/cronus-wz or Reference wz dumps).</summary>
public sealed class DirectoryWzStore : IWzStore
{
    private readonly string _root;

    public DirectoryWzStore(string root) => _root = root;

    public string? ReadText(string relativePath)
    {
        string path = Path.Combine(_root, relativePath);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    public bool Exists(string relativePath) => File.Exists(Path.Combine(_root, relativePath));

    public IEnumerable<string> EnumeratePaths(string prefix)
    {
        string dir = Path.Combine(_root, prefix.TrimEnd('/'));
        if (!Directory.Exists(dir))
        {
            yield break;
        }

        foreach (string file in Directory.EnumerateFiles(dir, "*.img.xml", SearchOption.AllDirectories))
        {
            yield return Path.GetRelativePath(_root, file).Replace('\\', '/');
        }
    }
}

/// <summary>
/// The game-data database (<c>gamedata.db</c>): one SQLite file holding every image as
/// deflate-compressed wz_xml, keyed by relative path — built by <c>Cronus.Ingest</c> from the
/// actual client, so the server is guaranteed to run on the same data the client renders.
/// Connections come from the provider pool per call; reads are thread-safe.
/// </summary>
public sealed class SqliteWzStore : IWzStore
{
    private readonly string _connectionString;

    public SqliteWzStore(string dbPath)
        => _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = true,
        }.ToString();

    /// <summary>The ingest metadata (source client, image count, timestamp) for logs.</summary>
    public IReadOnlyDictionary<string, string> Meta
    {
        get
        {
            var meta = new Dictionary<string, string>();
            using var db = new SqliteConnection(_connectionString);
            db.Open();
            using var cmd = new SqliteCommand("SELECT key, value FROM meta", db);
            using SqliteDataReader r = cmd.ExecuteReader();
            while (r.Read())
            {
                meta[r.GetString(0)] = r.GetString(1);
            }

            return meta;
        }
    }

    public string? ReadText(string relativePath)
    {
        using var db = new SqliteConnection(_connectionString);
        db.Open();
        using var cmd = new SqliteCommand("SELECT xml FROM wz_img WHERE path = $p", db);
        cmd.Parameters.AddWithValue("$p", Normalize(relativePath));
        if (cmd.ExecuteScalar() is not byte[] blob)
        {
            return null;
        }

        using var input = new MemoryStream(blob);
        using var inflate = new DeflateStream(input, CompressionMode.Decompress);
        using var reader = new StreamReader(inflate, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    public bool Exists(string relativePath)
    {
        using var db = new SqliteConnection(_connectionString);
        db.Open();
        using var cmd = new SqliteCommand("SELECT 1 FROM wz_img WHERE path = $p", db);
        cmd.Parameters.AddWithValue("$p", Normalize(relativePath));
        return cmd.ExecuteScalar() is not null;
    }

    public IEnumerable<string> EnumeratePaths(string prefix)
    {
        using var db = new SqliteConnection(_connectionString);
        db.Open();
        using var cmd = new SqliteCommand(
            "SELECT path FROM wz_img WHERE path GLOB $g", db);
        cmd.Parameters.AddWithValue("$g", Normalize(prefix.TrimEnd('/')) + "/*");
        using SqliteDataReader r = cmd.ExecuteReader();
        while (r.Read())
        {
            yield return r.GetString(0);
        }
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
