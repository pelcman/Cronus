// Cronus.WzTool — inspect and edit the client's .wz archives.
//
//   wz info    <file.wz>
//   wz ls      <file.wz> [path]                 directory listing, or an image's nodes
//   wz dump    <file.wz> <image path> [--out x.xml]
//   wz png     <file.wz> <canvas path> <out.png>
//   wz rewrite <file.wz> --out <new.wz>          rebuild unchanged (the round-trip check)
//   wz graft   <src.wz> <src path> <dst.wz> <dst path> --out <new.wz> [--no-verify]
//   wz verify  <a.wz> <b.wz>                     compare every image's parsed content
//   wz set-int <file.wz> <image glob> <node path> <value> --out <new.wz>   mass-edit one int
//
// A path is "<dir>/<name>.img[/node/node…]", e.g. Obj/vehicle.img/ship/ossyria/97.
using System.Diagnostics;
using System.Text;
using Cronus.Data.Wz;

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
Console.OutputEncoding = new UTF8Encoding(false);

if (args.Length == 0)
{
    return Usage();
}

try
{
    return args[0].ToLowerInvariant() switch
    {
        "info" => Info(args),
        "ls" => List(args),
        "dump" => Dump(args),
        "png" => Png(args),
        "rewrite" => Rewrite(args),
        "graft" => Graft(args),
        "verify" => Verify(args),
        "set-int" => SetInt(args),
        _ => Usage(),
    };
}
catch (Exception e) when (e is InvalidDataException or ArgumentException or FileNotFoundException or NotSupportedException)
{
    Console.Error.WriteLine($"error: {e.Message}");
    return 2;
}

static int Usage()
{
    Console.WriteLine("""
        usage:
          wz info    <file.wz>
          wz ls      <file.wz> [path]
          wz dump    <file.wz> <image path> [--out file.xml]
          wz png     <file.wz> <canvas path> <out.png>
          wz rewrite <file.wz> --out <new.wz>
          wz graft   <src.wz> <src path> <dst.wz> <dst path> --out <new.wz> [--no-verify]
          wz verify  <a.wz> <b.wz>
          wz set-int <file.wz> <image glob> <node path> <value> --out <new.wz> [--no-verify]
        path = <dir>/<name>.img[/node/…]   e.g. Obj/vehicle.img/ship/ossyria/97
        image glob = archive paths with * wildcards, e.g. "Map*/*.img" (every map)
        """);
    return 1;
}

static string? Option(string[] args, string name)
{
    int i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

static bool Flag(string[] args, string name) => Array.IndexOf(args, name) >= 0;

/// <summary>Splits "Obj/vehicle.img/ship/ossyria/97" into ("Obj/vehicle.img", "ship/ossyria/97").</summary>
static (string Image, string Node) SplitPath(string path)
{
    string[] parts = path.Replace('\\', '/').Trim('/').Split('/');
    int idx = Array.FindIndex(parts, p => p.EndsWith(".img", StringComparison.OrdinalIgnoreCase));
    if (idx < 0)
    {
        return (path, "");
    }

    return (string.Join('/', parts, 0, idx + 1), string.Join('/', parts, idx + 1, parts.Length - idx - 1));
}

static WzImageEntry RequireImage(WzArchive archive, string imagePath)
    => archive.FindImage(imagePath) ?? throw new FileNotFoundException($"{archive.BaseName}.wz has no image '{imagePath}'");

static int Info(string[] args)
{
    if (args.Length < 2)
    {
        return Usage();
    }

    using WzArchive a = WzArchive.Open(args[1]);
    int dirs = CountDirs(a.Root);
    Console.WriteLine($"{Path.GetFileName(args[1])}: v{a.Version} iv={a.IvName} header={a.FileStart} bytes, encVersion=0x{a.EncVersion:X4}");
    Console.WriteLine($"  size field {a.HeaderFileSize} (file {a.Length}; {(a.HeaderFileSize == (ulong)(a.Length - a.FileStart) ? "excludes" : a.HeaderFileSize == (ulong)a.Length ? "includes" : "does not match")} the header)");
    Console.WriteLine($"  {dirs} directories, {a.Images.Count} images");
    return 0;

    static int CountDirs(WzDirectory d) => d.Entries.Count(e => e.IsDirectory) + d.Entries.Where(e => e.IsDirectory).Sum(e => CountDirs(e.Directory!));
}

static int List(string[] args)
{
    if (args.Length < 2)
    {
        return Usage();
    }

    using WzArchive a = WzArchive.Open(args[1]);
    string path = args.Length >= 3 ? args[2].Replace('\\', '/').Trim('/') : "";
    (string imagePath, string nodePath) = SplitPath(path);
    if (path.Length > 0 && a.FindImage(imagePath) is { } image)
    {
        WzNode root = WzImageReader.Read(a, image);
        WzNode? node = nodePath.Length == 0 ? root : root.Find(nodePath);
        if (node is null)
        {
            throw new FileNotFoundException($"no node '{nodePath}' in {imagePath}");
        }

        Console.WriteLine($"{imagePath}/{nodePath}: {node.Describe()}");
        foreach (WzNode child in node.Children)
        {
            Console.WriteLine($"  {child.Name}: {child.Describe()}");
        }

        return 0;
    }

    WzDirectory dir = a.Root;
    foreach (string segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
    {
        dir = dir.Entries.FirstOrDefault(e => e.IsDirectory && string.Equals(e.Name, segment, StringComparison.OrdinalIgnoreCase))?.Directory
            ?? throw new FileNotFoundException($"no directory '{segment}' under '{path}'");
    }

    foreach (WzDirEntry entry in dir.Entries)
    {
        Console.WriteLine(entry.IsDirectory ? $"  {entry.Name}/" : $"  {entry.Name}  ({entry.Image!.Size} bytes)");
    }

    return 0;
}

static int Dump(string[] args)
{
    if (args.Length < 3)
    {
        return Usage();
    }

    using WzArchive a = WzArchive.Open(args[1]);
    (string imagePath, _) = SplitPath(args[2]);
    string xml = WzImageDumper.DumpXml(a, RequireImage(a, imagePath));
    string? out_ = Option(args, "--out");
    if (out_ is null)
    {
        Console.WriteLine(xml);
    }
    else
    {
        File.WriteAllText(out_, xml, new UTF8Encoding(false));
        Console.WriteLine($"wrote {out_} ({xml.Length} chars)");
    }

    return 0;
}

static int Png(string[] args)
{
    if (args.Length < 4)
    {
        return Usage();
    }

    using WzArchive a = WzArchive.Open(args[1]);
    (string imagePath, string nodePath) = SplitPath(args[2]);
    (WzNode root, WzCrypto crypto) = WzImageReader.ReadWithCrypto(a, RequireImage(a, imagePath));
    WzNode node = root.Find(nodePath) ?? throw new FileNotFoundException($"no node '{nodePath}' in {imagePath}");
    if (node.Kind != WzNodeKind.Canvas)
    {
        node = node.Children.FirstOrDefault(c => c.Kind == WzNodeKind.Canvas)
            ?? throw new InvalidDataException($"'{nodePath}' is {node.Describe()}, not a canvas");
    }

    byte[] bgra = WzCanvasCodec.DecodeBgra(node, crypto);
    File.WriteAllBytes(args[3], WzCanvasCodec.EncodePng(node.Width, node.Height, bgra));
    Console.WriteLine($"wrote {args[3]}: {node.Width}x{node.Height} format {node.Format}+{node.Format2}");
    return 0;
}

static int Rewrite(string[] args)
{
    string? out_ = Option(args, "--out");
    if (args.Length < 2 || out_ is null)
    {
        return Usage();
    }

    var sw = Stopwatch.StartNew();
    using (WzArchive a = WzArchive.Open(args[1]))
    {
        WzArchiveWriter.Rewrite(a, out_);
    }

    Console.WriteLine($"rewrote {args[1]} -> {out_} in {sw.Elapsed.TotalSeconds:F1}s");
    return VerifyFiles(args[1], out_, expectDifferent: null);
}

static int Graft(string[] args)
{
    string? out_ = Option(args, "--out");
    if (args.Length < 5 || out_ is null)
    {
        return Usage();
    }

    var sw = Stopwatch.StartNew();
    (string srcImagePath, string srcNodePath) = SplitPath(args[2]);
    (string dstImagePath, string dstNodePath) = SplitPath(args[4]);
    if (srcNodePath.Length == 0 || dstNodePath.Length == 0)
    {
        throw new ArgumentException("graft needs node paths inside the images (whole-image copies: use the parent directory + name as the node)");
    }

    using WzArchive src = WzArchive.Open(args[1]);
    using WzArchive dst = WzArchive.Open(args[3]);

    (WzNode srcRoot, WzCrypto srcCrypto) = WzImageReader.ReadWithCrypto(src, RequireImage(src, srcImagePath));
    WzNode graft = srcRoot.Find(srcNodePath) ?? throw new FileNotFoundException($"no node '{srcNodePath}' in {srcImagePath} of {src.BaseName}.wz");

    WzImageEntry dstImage = RequireImage(dst, dstImagePath);
    (WzNode dstRoot, WzCrypto dstCrypto) = WzImageReader.ReadWithCrypto(dst, dstImage);
    string[] dstParts = dstNodePath.Split('/');
    string parentPath = string.Join('/', dstParts, 0, dstParts.Length - 1);
    WzNode parent = parentPath.Length == 0 ? dstRoot : dstRoot.Find(parentPath)
        ?? throw new FileNotFoundException($"no node '{parentPath}' in {dstImagePath} of {dst.BaseName}.wz");

    // Canvas data may be XOR'd with the source archive's key; plain zlib is what every client
    // reads, so store that. Strings are re-encoded by the writer with the destination's key.
    int canvases = 0;
    foreach (WzNode node in graft.Descendants().Where(n => n.Kind == WzNodeKind.Canvas))
    {
        node.CanvasData = WzCanvasCodec.ToPlainZlib(node, srcCrypto);
        canvases++;
    }

    string leaf = dstParts[^1];
    WzNode? previous = parent.Child(leaf);
    graft.Name = leaf;
    parent.Put(graft);

    byte[] image = WzImageWriter.Serialize(dstRoot, dstCrypto);
    WzArchiveWriter.Rewrite(dst, out_, new Dictionary<string, byte[]> { [dstImage.ArchivePath] = image });

    Console.WriteLine($"grafted {src.BaseName}.wz/{args[2]} ({graft.Describe()}, {canvases} canvases)");
    Console.WriteLine($"     -> {dst.BaseName}.wz/{args[4]} (was: {previous?.Describe() ?? "absent"})");
    Console.WriteLine($"wrote {out_} in {sw.Elapsed.TotalSeconds:F1}s");

    if (Flag(args, "--no-verify"))
    {
        return 0;
    }

    return VerifyFiles(args[3], out_, expectDifferent: dstImage.ArchivePath);
}

static int Verify(string[] args)
{
    if (args.Length < 3)
    {
        return Usage();
    }

    return VerifyFiles(args[1], args[2], expectDifferent: null);
}

/// <summary>Sets (or adds) one int node in every image matching a glob — e.g. info/fly = 1 on
/// every map so the Flying temporary stat works everywhere, not just on the 72 fly maps.</summary>
static int SetInt(string[] args)
{
    string? out_ = Option(args, "--out");
    if (args.Length < 5 || out_ is null || !int.TryParse(args[4], out int value))
    {
        return Usage();
    }

    var sw = Stopwatch.StartNew();
    var glob = new System.Text.RegularExpressions.Regex(
        "^" + System.Text.RegularExpressions.Regex.Escape(args[2].Replace('\\', '/').Trim('/')).Replace("\\*", "[^/]*") + "$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    string[] nodePath = args[3].Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
    if (nodePath.Length == 0)
    {
        throw new ArgumentException("a node path is required (e.g. info/fly)");
    }

    using WzArchive a = WzArchive.Open(args[1]);
    var replaced = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
    int created = 0, updated = 0, unchanged = 0;
    foreach (WzImageEntry image in a.Images)
    {
        if (!glob.IsMatch(image.ArchivePath))
        {
            continue;
        }

        (WzNode root, WzCrypto crypto) = WzImageReader.ReadWithCrypto(a, image);
        WzNode parent = root;
        for (int i = 0; i < nodePath.Length - 1; i++)
        {
            WzNode? next = parent.Child(nodePath[i]);
            if (next is null)
            {
                next = new WzNode(nodePath[i], WzNodeKind.Property);
                parent.Children.Add(next);
            }

            parent = next;
        }

        WzNode? leaf = parent.Child(nodePath[^1]);
        if (leaf is null)
        {
            parent.Children.Add(new WzNode(nodePath[^1], WzNodeKind.Int) { TypeCode = 3, IntValue = value });
            created++;
        }
        else if (leaf.Kind is WzNodeKind.Int or WzNodeKind.Short or WzNodeKind.Long)
        {
            if (leaf.IntValue == value)
            {
                unchanged++;
                continue;
            }

            leaf.IntValue = value;
            updated++;
        }
        else
        {
            throw new InvalidDataException($"{image.ArchivePath}/{args[3]} is {leaf.Describe()}, not an int");
        }

        replaced[image.ArchivePath] = WzImageWriter.Serialize(root, crypto);
    }

    if (replaced.Count == 0)
    {
        Console.WriteLine($"nothing to change ({unchanged} already at {value})");
        return 0;
    }

    WzArchiveWriter.Rewrite(a, out_, replaced);
    Console.WriteLine($"set {args[3]} = {value}: {created} added, {updated} updated, {unchanged} already there; wrote {out_} in {sw.Elapsed.TotalSeconds:F1}s");
    if (Flag(args, "--no-verify"))
    {
        return 0;
    }

    return VerifyFilesSet(args[1], out_, new HashSet<string>(replaced.Keys, StringComparer.OrdinalIgnoreCase));
}

/// <summary>Parses every image of both archives and compares the results. When
/// <paramref name="expectDifferent"/> names an image, that one must differ (and still parse);
/// every other image must be identical.</summary>
static int VerifyFiles(string aPath, string bPath, string? expectDifferent)
    => VerifyFilesSet(aPath, bPath, expectDifferent is null ? null : new HashSet<string>(new[] { expectDifferent }, StringComparer.OrdinalIgnoreCase));

static int VerifyFilesSet(string aPath, string bPath, HashSet<string>? expectDifferentSet)
{
    var sw = Stopwatch.StartNew();
    using WzArchive a = WzArchive.Open(aPath);
    using WzArchive b = WzArchive.Open(bPath);
    Console.WriteLine($"verify: {Path.GetFileName(aPath)} (v{a.Version} iv={a.IvName}, {a.Images.Count} images) vs {Path.GetFileName(bPath)} (v{b.Version} iv={b.IvName}, {b.Images.Count} images)");

    var bImages = b.Images.ToDictionary(i => i.ArchivePath, StringComparer.OrdinalIgnoreCase);
    int same = 0, different = 0, missing = 0, failed = 0, changedAsIntended = 0;
    foreach (WzImageEntry image in a.Images)
    {
        if (!bImages.TryGetValue(image.ArchivePath, out WzImageEntry? other))
        {
            missing++;
            Console.WriteLine($"  [missing in b] {image.ArchivePath}");
            continue;
        }

        string xa, xb;
        try
        {
            xa = WzImageDumper.DumpXml(a, image);
            xb = WzImageDumper.DumpXml(b, other);
        }
        catch (Exception e) when (e is InvalidDataException or EndOfStreamException)
        {
            failed++;
            Console.WriteLine($"  [parse failed] {image.ArchivePath}: {e.Message}");
            continue;
        }

        bool isExpected = expectDifferentSet is not null && expectDifferentSet.Contains(image.ArchivePath);
        if (xa == xb)
        {
            same++;
            if (isExpected)
            {
                Console.WriteLine($"  [unchanged?] {image.ArchivePath} was expected to differ");
            }
        }
        else if (isExpected)
        {
            changedAsIntended++;
            if (changedAsIntended <= 5)
            {
                Console.WriteLine($"  [changed as intended] {image.ArchivePath}");
            }
        }
        else
        {
            different++;
            if (different <= 10)
            {
                Console.WriteLine($"  [different] {image.ArchivePath}");
            }
        }
    }

    int extra = b.Images.Count - (a.Images.Count - missing);
    Console.WriteLine($"verify: {same} identical, {changedAsIntended} changed as intended, {different} unexpectedly different, {missing} missing, {extra} extra, {failed} unparseable ({sw.Elapsed.TotalSeconds:F1}s)");
    bool ok = different == 0 && missing == 0 && extra == 0 && failed == 0
        && (expectDifferentSet is null || changedAsIntended == expectDifferentSet.Count);
    Console.WriteLine(ok ? "verify: OK" : "verify: FAILED");
    return ok ? 0 : 3;
}
