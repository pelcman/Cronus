using System.IO.Compression;
using System.Text;
using Cronus.Data.Wz;
using Xunit;

namespace Cronus.Data.Tests;

/// <summary>
/// The .wz writer against the .wz reader: an archive built from scratch must open (version and IV
/// auto-detected), its image must parse back to the same tree, and a canvas grafted between
/// archives with different keys must decode to the same pixels.
/// </summary>
public class WzRoundTripTests
{
    static WzRoundTripTests()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    private static readonly byte[] Pixels2x2 =
    {
        0x10, 0x20, 0x30, 0xFF,   // BGRA
        0x40, 0x50, 0x60, 0x80,
        0x70, 0x80, 0x90, 0x00,
        0xA0, 0xB0, 0xC0, 0xFF,
    };

    private static byte[] Zlib(byte[] raw)
    {
        using var ms = new MemoryStream();
        using (var z = new ZLibStream(ms, CompressionLevel.Optimal, leaveOpen: true))
        {
            z.Write(raw);
        }

        return ms.ToArray();
    }

    /// <summary>One of every node kind, with the edge cases the encodings have.</summary>
    private static WzNode SampleTree()
    {
        var root = new WzNode("vehicle.img", WzNodeKind.Property);
        var info = new WzNode("info", WzNodeKind.Property);
        info.Children.Add(new WzNode("small", WzNodeKind.Int) { TypeCode = 3, IntValue = 5 });
        info.Children.Add(new WzNode("edge", WzNodeKind.Int) { TypeCode = 3, IntValue = -128 });      // needs the long form
        info.Children.Add(new WzNode("big", WzNodeKind.Int) { TypeCode = 19, IntValue = 200090010 });
        info.Children.Add(new WzNode("neg", WzNodeKind.Short) { TypeCode = 2, IntValue = -195 });
        info.Children.Add(new WzNode("s11", WzNodeKind.Short) { TypeCode = 11, IntValue = 7 });
        info.Children.Add(new WzNode("wide", WzNodeKind.Long) { TypeCode = 20, IntValue = 5_000_000_000 });
        info.Children.Add(new WzNode("zero", WzNodeKind.Float) { TypeCode = 4, FloatValue = 0 });
        info.Children.Add(new WzNode("rate", WzNodeKind.Float) { TypeCode = 4, FloatValue = 1.5 });
        info.Children.Add(new WzNode("recovery", WzNodeKind.Double) { TypeCode = 5, FloatValue = 0.3 });
        info.Children.Add(new WzNode("mark", WzNodeKind.String) { TypeCode = 8, StringValue = "Ellinia" });
        info.Children.Add(new WzNode("name", WzNodeKind.String) { TypeCode = 8, StringValue = "テスト船" });       // MS932 8-bit
        info.Children.Add(new WzNode("uname", WzNodeKind.String) { TypeCode = 8, StringValue = "テスト", StringIsUnicode = true });
        info.Children.Add(new WzNode("long", WzNodeKind.String) { TypeCode = 8, StringValue = new string('x', 300) });
        info.Children.Add(new WzNode("empty", WzNodeKind.String) { TypeCode = 8, StringValue = "" });
        info.Children.Add(new WzNode("nothing", WzNodeKind.Null));
        info.Children.Add(new WzNode("mark", WzNodeKind.String) { TypeCode = 8, StringValue = "Ellinia" }); // dedup path
        root.Children.Add(info);

        var ship = new WzNode("ship", WzNodeKind.Property);
        var canvas = new WzNode("0", WzNodeKind.Canvas)
        {
            Width = 2,
            Height = 2,
            Format = 2,
            Format2 = 0,
            CanvasData = Zlib(Pixels2x2),
        };
        canvas.Children.Add(new WzNode("origin", WzNodeKind.Vector) { X = 419, Y = -292 });
        canvas.Children.Add(new WzNode("z", WzNodeKind.Int) { TypeCode = 3, IntValue = 0 });
        ship.Children.Add(canvas);
        ship.Children.Add(new WzNode("link", WzNodeKind.Uol) { StringValue = "../info/mark" });
        var convex = new WzNode("hull", WzNodeKind.Convex);
        convex.Children.Add(new WzNode("hull", WzNodeKind.Vector) { X = 1, Y = 2 });
        convex.Children.Add(new WzNode("hull", WzNodeKind.Vector) { X = -300, Y = 400 });
        ship.Children.Add(convex);
        ship.Children.Add(new WzNode("horn", WzNodeKind.Sound) { RawPayload = new byte[] { 0, 5, 9, 1, 2, 3, 4, 5 } });
        root.Children.Add(ship);
        return root;
    }

    private static void AssertSameTree(WzNode expected, WzNode actual)
    {
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Kind, actual.Kind);
        switch (expected.Kind)
        {
            case WzNodeKind.Short or WzNodeKind.Int or WzNodeKind.Long:
                Assert.Equal(expected.IntValue, actual.IntValue);
                Assert.Equal(expected.TypeCode, actual.TypeCode);
                break;
            case WzNodeKind.Float:
                Assert.Equal((float)expected.FloatValue, (float)actual.FloatValue);
                break;
            case WzNodeKind.Double:
                Assert.Equal(expected.FloatValue, actual.FloatValue);
                break;
            case WzNodeKind.String or WzNodeKind.Uol:
                Assert.Equal(expected.StringValue, actual.StringValue);
                Assert.Equal(expected.StringIsUnicode, actual.StringIsUnicode);
                break;
            case WzNodeKind.Vector:
                Assert.Equal((expected.X, expected.Y), (actual.X, actual.Y));
                break;
            case WzNodeKind.Canvas:
                Assert.Equal((expected.Width, expected.Height, expected.Format, expected.Format2), (actual.Width, actual.Height, actual.Format, actual.Format2));
                Assert.Equal(expected.CanvasData, actual.CanvasData);
                break;
            case WzNodeKind.Sound:
                Assert.Equal(expected.RawPayload, actual.RawPayload);
                break;
        }

        Assert.Equal(expected.Children.Count, actual.Children.Count);
        for (int i = 0; i < expected.Children.Count; i++)
        {
            AssertSameTree(expected.Children[i], actual.Children[i]);
        }
    }

    private static string WriteArchive(int version, WzCrypto crypto, WzNode image, string imageName = "vehicle.img")
    {
        var root = new WzDirModel("");
        root.AddImage("Base.img", WzImageWriter.Serialize(new WzNode("Base.img", WzNodeKind.Property), crypto));
        WzDirModel obj = root.AddDirectory("Obj");
        obj.AddImage(imageName, WzImageWriter.Serialize(image, crypto));
        WzDirModel deeper = obj.AddDirectory("deeper");
        deeper.AddImage("x.img", WzImageWriter.Serialize(new WzNode("x.img", WzNodeKind.Property), crypto));

        string path = Path.Combine(Path.GetTempPath(), $"cronus-wz-{Guid.NewGuid():N}.wz");
        WzArchiveWriter.Write(WzArchiveModel.Create(version, crypto, root), path);
        return path;
    }

    [Theory]
    [InlineData("none", 186)]
    [InlineData("gms", 83)]
    [InlineData("ems", 62)]
    public void WrittenArchive_OpensAndParsesBackToTheSameTree(string ivName, int version)
    {
        WzCrypto crypto = new(WzCrypto.KnownIvs.First(k => k.Name == ivName).Iv);
        WzNode tree = SampleTree();
        string path = WriteArchive(version, crypto, tree);
        try
        {
            using WzArchive archive = WzArchive.Open(path);
            Assert.Equal(version, archive.Version);
            Assert.Equal(ivName, archive.IvName);
            Assert.Equal(3, archive.Images.Count);
            Assert.Equal(new[] { "Base.img", "Obj/vehicle.img", "Obj/deeper/x.img" }, archive.Images.Select(i => i.ArchivePath).ToArray());
            Assert.Equal((ulong)(archive.Length - archive.FileStart), archive.HeaderFileSize);

            WzImageEntry image = archive.FindImage("Obj/vehicle.img")!;
            WzNode parsed = WzImageReader.Read(archive, image);
            AssertSameTree(tree, parsed);

            // The dumper (the ingest's path) reads it too, and sees the same shape.
            string xml = WzImageDumper.DumpXml(archive, image);
            Assert.Contains("<canvas name=\"0\" width=\"2\" height=\"2\">", xml);
            Assert.Contains("<string name=\"name\" value=\"テスト船\"/>", xml);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Rewrite_ReproducesEveryImageByteForByte()
    {
        WzCrypto crypto = new(WzCrypto.KnownIvs[1].Iv);
        string original = WriteArchive(83, crypto, SampleTree());
        string copy = original + ".rewrite.wz";
        try
        {
            using (WzArchive a = WzArchive.Open(original))
            {
                WzArchiveWriter.Rewrite(a, copy);
            }

            using WzArchive src = WzArchive.Open(original);
            using WzArchive dst = WzArchive.Open(copy);
            Assert.Equal(src.Version, dst.Version);
            Assert.Equal(src.IvName, dst.IvName);
            Assert.Equal(src.Images.Count, dst.Images.Count);
            foreach (WzImageEntry image in src.Images)
            {
                WzImageEntry other = dst.FindImage(image.ArchivePath)!;
                Assert.Equal(src.ReadImageBytes(image), dst.ReadImageBytes(other));
            }

            Assert.Equal(File.ReadAllBytes(original), File.ReadAllBytes(copy)); // same layout, same bytes
        }
        finally
        {
            File.Delete(original);
            File.Delete(copy);
        }
    }

    [Fact]
    public void Graft_AcrossKeys_DecodesToTheSamePixels()
    {
        // Source: GMS key; destination: no key (JMS). The canvas must survive the move.
        var gms = new WzCrypto(WzCrypto.KnownIvs[1].Iv);
        var none = new WzCrypto(WzCrypto.KnownIvs[0].Iv);
        string srcPath = WriteArchive(83, gms, SampleTree());
        var dstTree = new WzNode("vehicle.img", WzNodeKind.Property);
        var ship = new WzNode("ship", WzNodeKind.Property);
        ship.Children.Add(new WzNode("0", WzNodeKind.Canvas) { Width = 1, Height = 1, Format = 2, CanvasData = Zlib(new byte[] { 0, 0, 0, 0 }) });
        dstTree.Children.Add(ship);
        string dstPath = WriteArchive(186, none, dstTree);
        string outPath = dstPath + ".graft.wz";
        try
        {
            using (WzArchive src = WzArchive.Open(srcPath))
            using (WzArchive dst = WzArchive.Open(dstPath))
            {
                (WzNode srcRoot, WzCrypto srcCrypto) = WzImageReader.ReadWithCrypto(src, src.FindImage("Obj/vehicle.img")!);
                WzNode graft = srcRoot.Find("ship/0")!;
                foreach (WzNode c in graft.Descendants().Where(n => n.Kind == WzNodeKind.Canvas))
                {
                    c.CanvasData = WzCanvasCodec.ToPlainZlib(c, srcCrypto);
                }

                WzImageEntry dstImage = dst.FindImage("Obj/vehicle.img")!;
                (WzNode dstRoot, WzCrypto dstCrypto) = WzImageReader.ReadWithCrypto(dst, dstImage);
                dstRoot.Find("ship")!.Put(graft);
                WzArchiveWriter.Rewrite(dst, outPath, new Dictionary<string, byte[]>
                {
                    [dstImage.ArchivePath] = WzImageWriter.Serialize(dstRoot, dstCrypto),
                });
            }

            using WzArchive result = WzArchive.Open(outPath);
            Assert.Equal("none", result.IvName);
            (WzNode root, WzCrypto crypto) = WzImageReader.ReadWithCrypto(result, result.FindImage("Obj/vehicle.img")!);
            WzNode canvas = root.Find("ship/0")!;
            Assert.Equal((2, 2), (canvas.Width, canvas.Height));
            Assert.Equal(419, canvas.Child("origin")!.X);
            Assert.Equal(Pixels2x2, WzCanvasCodec.DecodeBgra(canvas, crypto));
        }
        finally
        {
            File.Delete(srcPath);
            File.Delete(dstPath);
            File.Delete(outPath);
        }
    }

    [Fact]
    public void Codec_DecodesFormatsAndWritesPng()
    {
        var none = new WzCrypto(WzCrypto.KnownIvs[0].Iv);
        var bgra8888 = new WzNode("0", WzNodeKind.Canvas) { Width = 2, Height = 2, Format = 2, CanvasData = Zlib(Pixels2x2) };
        Assert.Equal(Pixels2x2, WzCanvasCodec.DecodeBgra(bgra8888, none));

        // BGRA4444: 0xF00F = a15 r0 g0 b15 -> B 255, G 0, R 0, A 255
        var bgra4444 = new WzNode("0", WzNodeKind.Canvas) { Width = 1, Height = 1, Format = 1, CanvasData = Zlib(new byte[] { 0x0F, 0xF0 }) };
        Assert.Equal(new byte[] { 255, 0, 0, 255 }, WzCanvasCodec.DecodeBgra(bgra4444, none));

        // RGB565: 0xF800 = pure red
        var rgb565 = new WzNode("0", WzNodeKind.Canvas) { Width = 1, Height = 1, Format = 513, CanvasData = Zlib(new byte[] { 0x00, 0xF8 }) };
        Assert.Equal(new byte[] { 0, 0, 255, 255 }, WzCanvasCodec.DecodeBgra(rgb565, none));

        byte[] png = WzCanvasCodec.EncodePng(2, 2, Pixels2x2);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, png.Take(4).ToArray());
        Assert.Equal("IEND", Encoding.ASCII.GetString(png, png.Length - 8, 4));
    }

    [Fact]
    public void Codec_UndoesKeyedChunks()
    {
        var gms = new WzCrypto(WzCrypto.KnownIvs[1].Iv);
        byte[] zlib = Zlib(Pixels2x2);
        var chunked = new MemoryStream();
        var w = new BinaryWriter(chunked);
        int half = zlib.Length / 2;
        foreach ((int from, int count) in new[] { (0, half), (half, zlib.Length - half) })
        {
            w.Write(count);
            for (int i = 0; i < count; i++)
            {
                w.Write((byte)(zlib[from + i] ^ gms.KeyAt(i)));
            }
        }

        var canvas = new WzNode("0", WzNodeKind.Canvas) { Width = 2, Height = 2, Format = 2, CanvasData = chunked.ToArray() };
        Assert.False(WzCanvasCodec.IsPlainZlib(canvas.CanvasData));
        Assert.Equal(zlib, WzCanvasCodec.ToPlainZlib(canvas, gms));
        Assert.Equal(Pixels2x2, WzCanvasCodec.DecodeBgra(canvas, gms));
    }
}
