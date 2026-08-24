// Cronus.Ingest — builds the game-data database straight from a client's .wz files
// (the Maple2.File.Ingest idea). The core lives in Cronus.Data (WzIngest) so the server
// host can auto-ingest too; this console is the manual/CI entry point plus a verifier.
//
//   dotnet run --project src/Cronus.Ingest -- <client dir> [--out gamedata.db]
//   dotnet run --project src/Cronus.Ingest -- <client dir> --verify <wz_xml dump dir>
using System.Text;
using Cronus.Data.Wz;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

string? clientDir = null;
string outPath = "gamedata.db";
string? verifyDir = null;
for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--out" when i + 1 < args.Length:
            outPath = args[++i];
            break;
        case "--verify" when i + 1 < args.Length:
            verifyDir = args[++i];
            break;
        default:
            clientDir ??= args[i];
            break;
    }
}

if (clientDir is null || !Directory.Exists(clientDir))
{
    Console.WriteLine("usage: Cronus.Ingest <client dir with .wz files> [--out gamedata.db] [--verify <wz_xml dump dir>]");
    return 1;
}

if (verifyDir is not null)
{
    return Verify(clientDir, verifyDir);
}

WzIngest.BuildDatabase(clientDir, outPath, Console.WriteLine);
Console.WriteLine($"database: {new FileInfo(outPath).Length / 1024 / 1024} MB at {outPath}");
return 0;

// ---- --verify: byte-compare generated XML against an existing wz_xml dump ----------------
static int Verify(string clientDir, string dumpDir)
{
    int same = 0, different = 0, missingInWz = 0;
    foreach (string wzName in WzIngest.ArchiveNames)
    {
        string wzPath = Path.Combine(clientDir, wzName + ".wz");
        if (!File.Exists(wzPath))
        {
            continue;
        }

        using WzArchive archive = WzArchive.Open(wzPath);
        var generated = new Dictionary<string, WzImageEntry>();
        foreach (WzImageEntry image in archive.Images)
        {
            generated[WzIngest.StorePath(archive.BaseName, image)] = image;
        }

        string dumpRoot = Path.Combine(dumpDir, wzName);
        if (Directory.Exists(dumpRoot))
        {
            foreach (string file in Directory.EnumerateFiles(dumpRoot, "*.img.xml", SearchOption.AllDirectories))
            {
                string rel = Path.GetRelativePath(dumpDir, file).Replace('\\', '/');
                if (!generated.TryGetValue(rel, out WzImageEntry? entry))
                {
                    missingInWz++;
                    if (missingInWz <= 5)
                    {
                        Console.WriteLine($"[dump-only] {rel}");
                    }

                    continue;
                }

                string expected = File.ReadAllText(file);
                string actual;
                try
                {
                    actual = WzImageDumper.DumpXml(archive, entry);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"[parse-fail] {rel}: {e.Message}");
                    different++;
                    continue;
                }

                if (actual == expected)
                {
                    same++;
                }
                else
                {
                    different++;
                    if (different <= 5)
                    {
                        int at = FirstDiff(expected, actual);
                        Console.WriteLine($"[diff] {rel} at char {at}:");
                        Console.WriteLine($"   dump: ...{Snippet(expected, at)}...");
                        Console.WriteLine($"   ours: ...{Snippet(actual, at)}...");
                    }
                }
            }
        }

        Console.WriteLine($"[verify] {wzName}.wz v{archive.Version} iv={archive.IvName}: {same} same, {different} diff, {missingInWz} dump-only so far");
    }

    Console.WriteLine($"verify total: {same} identical, {different} different, {missingInWz} dump-only");
    return different == 0 && missingInWz == 0 ? 0 : 2;

    static int FirstDiff(string a, string b)
    {
        int n = Math.Min(a.Length, b.Length);
        for (int i = 0; i < n; i++)
        {
            if (a[i] != b[i])
            {
                return i;
            }
        }

        return n;
    }

    static string Snippet(string s, int at)
    {
        int from = Math.Max(0, at - 40);
        return s.Substring(from, Math.Min(100, s.Length - from));
    }
}
