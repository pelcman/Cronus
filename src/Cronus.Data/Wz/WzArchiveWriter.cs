using System.Text;

namespace Cronus.Data.Wz;

/// <summary>A directory to write: its entries in order.</summary>
public sealed class WzDirModel
{
    public WzDirModel(string name) => Name = name;

    public string Name { get; }

    public List<WzEntryModel> Entries { get; } = new();

    public WzDirModel AddDirectory(string name)
    {
        var dir = new WzDirModel(name);
        Entries.Add(new WzEntryModel(name, dir, null, 0));
        return dir;
    }

    public void AddImage(string name, byte[] bytes)
        => Entries.Add(new WzEntryModel(name, null, () => bytes, bytes.Length));
}

/// <summary>One entry to write: a sub-directory, or an image whose bytes are produced on demand
/// (so a 600 MB archive is streamed image by image, never held in memory at once).</summary>
public sealed record WzEntryModel(string Name, WzDirModel? Directory, Func<byte[]>? ImageData, int ImageSize)
{
    public bool IsDirectory => Directory is not null;
}

/// <summary>Everything <see cref="WzArchiveWriter"/> needs to produce a .wz file.</summary>
public sealed class WzArchiveModel
{
    /// <summary>The raw header: "PKG1", the 64-bit size (patched on write), the header size, the copyright.</summary>
    public required byte[] Header { get; init; }

    /// <summary>The 16-bit encoded version that follows the header.</summary>
    public required ushort EncVersion { get; init; }

    /// <summary>The version hash keying every offset.</summary>
    public required uint VersionHash { get; init; }

    /// <summary>The string crypto for directory names.</summary>
    public required WzCrypto Crypto { get; init; }

    public required WzDirModel Root { get; init; }

    /// <summary>Whether the header's size field counts the data region only (Nexon's files) or the whole file.</summary>
    public bool SizeExcludesHeader { get; init; } = true;

    /// <summary>
    /// A model that reproduces <paramref name="archive"/>: same header, version, crypto, and
    /// directory order; every image copied byte for byte except those in
    /// <paramref name="replacedImages"/> (keyed by archive path, e.g. "Obj/vehicle.img").
    /// </summary>
    public static WzArchiveModel FromArchive(WzArchive archive, IReadOnlyDictionary<string, byte[]>? replacedImages = null)
    {
        var replaced = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        if (replacedImages is not null)
        {
            foreach ((string key, byte[] value) in replacedImages)
            {
                replaced[key.Replace('\\', '/').Trim('/')] = value;
            }
        }

        var unused = new HashSet<string>(replaced.Keys, StringComparer.OrdinalIgnoreCase);
        WzDirModel root = Convert(archive.Root);
        if (unused.Count > 0)
        {
            throw new ArgumentException("replacement images not found in the archive: " + string.Join(", ", unused));
        }

        return new WzArchiveModel
        {
            Header = archive.ReadHeaderBytes(),
            EncVersion = archive.EncVersion,
            VersionHash = archive.VersionHash,
            Crypto = archive.Crypto,
            Root = root,
            SizeExcludesHeader = archive.HeaderFileSize != (ulong)archive.Length,
        };

        WzDirModel Convert(WzDirectory dir)
        {
            var model = new WzDirModel(dir.Name);
            foreach (WzDirEntry entry in dir.Entries)
            {
                if (entry.Directory is { } sub)
                {
                    model.Entries.Add(new WzEntryModel(sub.Name, Convert(sub), null, 0));
                }
                else
                {
                    WzImageEntry image = entry.Image!;
                    if (replaced.TryGetValue(image.ArchivePath, out byte[]? bytes))
                    {
                        unused.Remove(image.ArchivePath);
                        model.Entries.Add(new WzEntryModel(image.Name, null, () => bytes, bytes.Length));
                    }
                    else
                    {
                        model.Entries.Add(new WzEntryModel(image.Name, null, () => archive.ReadImageBytes(image), image.Size));
                    }
                }
            }

            return model;
        }
    }

    /// <summary>A model for a brand-new archive of the given game version and IV.</summary>
    public static WzArchiveModel Create(int version, WzCrypto crypto, WzDirModel root,
        string copyright = "Package file v1.0 Copyright 2002 Wizet, ZMS")
    {
        (byte encVersion, uint hash) = WzArchive.HashVersion(version);
        byte[] text = Encoding.ASCII.GetBytes(copyright);
        var header = new MemoryStream();
        var w = new BinaryWriter(header);
        w.Write(0x31474B50u);                      // "PKG1"
        w.Write(0UL);                              // size, patched on write
        w.Write((uint)(4 + 8 + 4 + text.Length + 1));
        w.Write(text);
        w.Write((byte)0);
        return new WzArchiveModel
        {
            Header = header.ToArray(),
            EncVersion = encVersion,
            VersionHash = hash,
            Crypto = crypto,
            Root = root,
        };
    }
}

/// <summary>
/// Writes a .wz archive from a <see cref="WzArchiveModel"/> in the layout MapleLib-era tools
/// (and the clients) use: header, encoded version, every directory's entry list (root first, then
/// sub-directories depth-first), then every image's bytes in directory order. Offsets are
/// position-keyed with the version hash; directory names use the archive's keystream.
/// </summary>
public static class WzArchiveWriter
{
    private sealed class DirPlan
    {
        public required WzDirModel Model { get; init; }
        public List<object> Children { get; } = new(); // DirPlan | ImagePlan, in entry order
        public long Offset { get; set; }
        public int EntryBlockSize { get; set; }
        public long DataSize { get; set; }
        public int Checksum { get; set; }
    }

    private sealed class ImagePlan
    {
        public required WzEntryModel Model { get; init; }
        public long Offset { get; set; }
        public int Checksum { get; set; }
    }

    /// <summary>Convenience: rewrite <paramref name="source"/> to <paramref name="outPath"/> with some images replaced.</summary>
    public static void Rewrite(WzArchive source, string outPath, IReadOnlyDictionary<string, byte[]>? replacedImages = null)
        => Write(WzArchiveModel.FromArchive(source, replacedImages), outPath);

    public static void Write(WzArchiveModel model, string outPath)
    {
        uint fileStart = (uint)model.Header.Length;

        // Plan: sizes first (independent of positions), then positions.
        var dirs = new List<DirPlan>();
        var images = new List<ImagePlan>();
        DirPlan root = Plan(model.Root, model.Crypto, dirs, images);

        long pos = fileStart + 2;
        foreach (DirPlan dir in dirs)
        {
            dir.Offset = pos;
            pos += dir.EntryBlockSize;
        }

        foreach (ImagePlan image in images)
        {
            image.Offset = pos;
            pos += image.Model.ImageSize;
        }

        using var fs = new FileStream(outPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 1 << 20);
        var w = new BinaryWriter(fs);
        w.Write(model.Header);
        fs.Position = fileStart;
        w.Write(model.EncVersion);

        // Directory blocks with zero checksums (their width is fixed, so nothing shifts later).
        foreach (DirPlan dir in dirs)
        {
            WriteDirectoryBlock(fs, w, dir, model, fileStart);
        }

        // Images, computing checksums as they stream through.
        foreach (ImagePlan image in images)
        {
            if (fs.Position != image.Offset)
            {
                throw new InvalidDataException($"layout drift before image '{image.Model.Name}': at {fs.Position}, planned {image.Offset}");
            }

            byte[] bytes = image.Model.ImageData!();
            if (bytes.Length != image.Model.ImageSize)
            {
                throw new InvalidDataException($"image '{image.Model.Name}' produced {bytes.Length} bytes, planned {image.Model.ImageSize}");
            }

            image.Checksum = Checksum(bytes);
            w.Write(bytes);
        }

        // Directory checksums are the sums of their children; fill them in now.
        ComputeChecksums(root);
        foreach (DirPlan dir in dirs)
        {
            fs.Position = dir.Offset;
            WriteDirectoryBlock(fs, w, dir, model, fileStart);
        }

        // The header's size field.
        long total = fs.Length;
        fs.Position = 4;
        w.Write((ulong)(model.SizeExcludesHeader ? total - fileStart : total));
        w.Flush();
    }

    private static DirPlan Plan(WzDirModel dir, WzCrypto crypto, List<DirPlan> dirs, List<ImagePlan> images)
    {
        var plan = new DirPlan { Model = dir };
        dirs.Add(plan);

        int block = WzImageWriter.CompressedIntLength(dir.Entries.Count);
        long data = 0;
        var subPlans = new List<(WzEntryModel Entry, WzDirModel Sub)>();
        foreach (WzEntryModel entry in dir.Entries)
        {
            if (entry.Directory is { } sub)
            {
                subPlans.Add((entry, sub));
                plan.Children.Add(null!); // placeholder, filled below in order
            }
            else
            {
                var image = new ImagePlan { Model = entry };
                plan.Children.Add(image);
                data += entry.ImageSize;
                block += 1 + WzImageWriter.WzStringLength(entry.Name, unicode: false)
                    + WzImageWriter.CompressedIntLength(entry.ImageSize) + 5 + 4;
            }
        }

        // Sub-directories are planned after this block's own images so their blocks follow ours.
        int subIndex = 0;
        for (int i = 0; i < plan.Children.Count; i++)
        {
            if (plan.Children[i] is null)
            {
                (WzEntryModel entry, WzDirModel sub) = subPlans[subIndex++];
                DirPlan subPlan = Plan(sub, crypto, dirs, images);
                plan.Children[i] = subPlan;
                data += subPlan.DataSize;
                block += 1 + WzImageWriter.WzStringLength(entry.Name, unicode: false)
                    + WzImageWriter.CompressedIntLength(checked((int)Math.Min(subPlan.DataSize, int.MaxValue))) + 5 + 4;
            }
        }

        // Image data is laid out in entry order after all directory blocks.
        foreach (object child in plan.Children)
        {
            if (child is ImagePlan image)
            {
                images.Add(image);
            }
        }

        plan.EntryBlockSize = block;
        plan.DataSize = data;
        return plan;
    }

    private static void ComputeChecksums(DirPlan dir)
    {
        int sum = 0;
        foreach (object child in dir.Children)
        {
            if (child is DirPlan sub)
            {
                ComputeChecksums(sub);
                sum = unchecked(sum + sub.Checksum);
            }
            else
            {
                sum = unchecked(sum + ((ImagePlan)child).Checksum);
            }
        }

        dir.Checksum = sum;
    }

    private static void WriteDirectoryBlock(FileStream fs, BinaryWriter w, DirPlan dir, WzArchiveModel model, uint fileStart)
    {
        if (fs.Position != dir.Offset)
        {
            throw new InvalidDataException($"layout drift before directory '{dir.Model.Name}': at {fs.Position}, planned {dir.Offset}");
        }

        WzImageWriter.WriteCompressedInt(w, dir.Children.Count);
        foreach (object child in dir.Children)
        {
            if (child is DirPlan sub)
            {
                w.Write((byte)3);
                WzImageWriter.WriteWzString(w, sub.Model.Name, model.Crypto, unicode: false);
                WzImageWriter.WriteCompressedInt(w, checked((int)Math.Min(sub.DataSize, int.MaxValue)));
                w.Write((sbyte)-128);
                w.Write(sub.Checksum);
                WriteEncryptedOffset(fs, w, (uint)sub.Offset, fileStart, model.VersionHash);
            }
            else
            {
                var image = (ImagePlan)child;
                w.Write((byte)4);
                WzImageWriter.WriteWzString(w, image.Model.Name, model.Crypto, unicode: false);
                WzImageWriter.WriteCompressedInt(w, image.Model.ImageSize);
                w.Write((sbyte)-128);
                w.Write(image.Checksum);
                WriteEncryptedOffset(fs, w, (uint)image.Offset, fileStart, model.VersionHash);
            }
        }

        if (fs.Position != dir.Offset + dir.EntryBlockSize)
        {
            throw new InvalidDataException($"directory '{dir.Model.Name}' block is {fs.Position - dir.Offset} bytes, planned {dir.EntryBlockSize}");
        }
    }

    /// <summary>The inverse of <see cref="WzArchive.ReadEncryptedOffset"/> at the current position.</summary>
    private static void WriteEncryptedOffset(FileStream fs, BinaryWriter w, uint target, uint fileStart, uint versionHash)
    {
        uint pos = (uint)fs.Position;
        uint key = (pos - fileStart) ^ 0xFFFFFFFF;
        unchecked
        {
            key *= versionHash;
            key -= 0x581C3F6D;
        }

        int rotate = (int)(key & 0x1F);
        key = (key << rotate) | (key >> (32 - rotate));
        uint value;
        unchecked
        {
            value = key ^ (target - fileStart * 2);
        }

        w.Write(value);
    }

    /// <summary>The directory-entry checksum: the byte sum of the image (wrapping), as MapleLib computes it.</summary>
    public static int Checksum(ReadOnlySpan<byte> bytes)
    {
        int sum = 0;
        foreach (byte b in bytes)
        {
            sum = unchecked(sum + b);
        }

        return sum;
    }
}
