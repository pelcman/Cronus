using System.Text;

namespace Cronus.Data.Wz;

/// <summary>One .img inside an archive: its directory path (e.g. "Map/Map1"), name, and offset.</summary>
public sealed record WzImageEntry(string Directory, string Name, uint Offset, int Size)
{
    /// <summary>The dump-style relative path: directory + name + ".xml".</summary>
    public string RelativePath => Directory.Length == 0 ? Name + ".xml" : Directory + "/" + Name + ".xml";

    /// <summary>The archive-relative path without the ".xml": "Obj/vehicle.img".</summary>
    public string ArchivePath => Directory.Length == 0 ? Name : Directory + "/" + Name;
}

/// <summary>A directory of the archive, with its entries in the order the file lists them.</summary>
public sealed class WzDirectory
{
    public WzDirectory(string name) => Name = name;

    public string Name { get; }

    /// <summary>Sub-directories and images, in file order (the rewrite keeps it).</summary>
    public List<WzDirEntry> Entries { get; } = new();
}

/// <summary>One directory entry: either a sub-directory or an image.</summary>
public sealed record WzDirEntry(WzDirectory? Directory, WzImageEntry? Image)
{
    public bool IsDirectory => Directory is not null;

    public string Name => Directory?.Name ?? Image!.Name;
}

/// <summary>
/// Reads a binary <c>.wz</c> archive (the classic pre-2021 format every JMS v186 file uses):
/// header, brute-forced version hash, encrypted names and offsets, and the directory tree.
/// The encryption IV and the version are auto-detected by validating a full directory parse —
/// the header stores only a hash, and several (iv, version) pairs collide on it.
/// </summary>
public sealed class WzArchive : IDisposable
{
    private readonly FileStream _file;
    private readonly BinaryReader _reader;
    private readonly long _length;

    private WzCrypto _crypto = null!;
    private WzCrypto _imageCrypto = null!;
    private uint _versionHash;

    private WzArchive(string path)
    {
        _file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16);
        _reader = new BinaryReader(_file);
        _length = _file.Length;
        BaseName = Path.GetFileNameWithoutExtension(path);
        FilePath = path;
    }

    /// <summary>The archive's base name ("Map" for Map.wz) — the first path segment in dumps.</summary>
    public string BaseName { get; }

    /// <summary>The file this archive was opened from.</summary>
    public string FilePath { get; }

    /// <summary>The file length in bytes.</summary>
    public long Length => _length;

    /// <summary>Where the data region starts (header size); offsets are relative to this.</summary>
    public uint FileStart { get; private set; }

    /// <summary>The 64-bit size field of the header (the data region's length in Nexon's files).</summary>
    public ulong HeaderFileSize { get; private set; }

    /// <summary>The 16-bit encoded version that follows the header.</summary>
    public ushort EncVersion { get; private set; }

    /// <summary>The detected game version (e.g. 186) — informational once parsing succeeds.</summary>
    public int Version { get; private set; }

    /// <summary>The name of the IV that decoded the archive ("none" / "gms" / "ems").</summary>
    public string IvName { get; private set; } = "";

    /// <summary>Every image in the archive, in directory order.</summary>
    public IReadOnlyList<WzImageEntry> Images { get; private set; } = Array.Empty<WzImageEntry>();

    /// <summary>The directory tree as the file lists it (what a rewrite reproduces).</summary>
    public WzDirectory Root { get; private set; } = new("");

    /// <summary>The version hash that keys the offsets (what a rewrite must reuse).</summary>
    internal uint VersionHash => _versionHash;

    /// <summary>The archive's string crypto (directory names and non-List.wz images).</summary>
    internal WzCrypto Crypto => _crypto;

    public static WzArchive Open(string path)
    {
        var archive = new WzArchive(path);
        try
        {
            archive.Parse();
            return archive;
        }
        catch
        {
            archive.Dispose();
            throw;
        }
    }

    public void Dispose() => _reader.Dispose();

    /// <summary>The raw header bytes [0, FileStart): magic, size, header size, copyright.</summary>
    public byte[] ReadHeaderBytes()
    {
        _file.Position = 0;
        return _reader.ReadBytes((int)FileStart);
    }

    /// <summary>The raw bytes of one image, exactly as stored (strings and pixels still encoded).</summary>
    public byte[] ReadImageBytes(WzImageEntry image)
    {
        _file.Position = image.Offset;
        return _reader.ReadBytes(image.Size);
    }

    /// <summary>Finds an image by its archive path ("Obj/vehicle.img"), or null.</summary>
    public WzImageEntry? FindImage(string archivePath)
    {
        string wanted = archivePath.Replace('\\', '/').Trim('/');
        return Images.FirstOrDefault(i => string.Equals(i.ArchivePath, wanted, StringComparison.OrdinalIgnoreCase));
    }

    // ---- header + (iv, version) detection ------------------------------------------------

    private void Parse()
    {
        _file.Position = 0;
        if (_reader.ReadUInt32() != 0x31474B50) // "PKG1"
        {
            throw new InvalidDataException("not a WZ archive (missing PKG1 magic)");
        }

        HeaderFileSize = _reader.ReadUInt64();
        FileStart = _reader.ReadUInt32();

        _file.Position = FileStart;
        int encVer = _reader.ReadUInt16();
        EncVersion = (ushort)encVer;

        foreach ((string ivName, byte[] iv) in WzCrypto.KnownIvs)
        {
            var crypto = new WzCrypto(iv);
            for (int version = 1; version < 1000; version++)
            {
                (byte enc, uint hash) = HashVersion(version);
                if (enc != encVer)
                {
                    continue;
                }

                _crypto = crypto;
                _imageCrypto = crypto;
                _versionHash = hash;
                try
                {
                    var images = new List<WzImageEntry>(1024);
                    var root = new WzDirectory("");
                    ParseDirectory(FileStart + 2, "", root, images, depth: 0);
                    if (images.Count == 0 || !LooksLikeImage(images[0]))
                    {
                        continue;
                    }

                    Version = version;
                    IvName = ivName;
                    Images = images;
                    Root = root;
                    return;
                }
                catch (InvalidDataException)
                {
                    // wrong candidate — names or offsets didn't validate; try the next
                }
                catch (EndOfStreamException)
                {
                }
                catch (IOException)
                {
                }
            }
        }

        throw new InvalidDataException("no (iv, version) candidate produced a valid directory");
    }

    /// <summary>The header's version byte and the offset hash for a game version (ports
    /// <c>WzTool.GetVersionHash</c>): hash = Σ (hash * 32 + digit + 1), byte = 0xFF ^ its four bytes.</summary>
    internal static (byte EncVer, uint Hash) HashVersion(int version)
    {
        uint hash = 0;
        foreach (char c in version.ToString())
        {
            hash = hash * 32 + c + 1;
        }

        byte enc = (byte)(0xFF
            ^ ((hash >> 24) & 0xFF)
            ^ ((hash >> 16) & 0xFF)
            ^ ((hash >> 8) & 0xFF)
            ^ (hash & 0xFF));
        return (enc, hash);
    }

    /// <summary>An image body must start with an inline or offset string block marker.</summary>
    private bool LooksLikeImage(WzImageEntry image)
    {
        if (image.Offset >= _length)
        {
            return false;
        }

        _file.Position = image.Offset;
        byte first = _reader.ReadByte();
        return first is 0x73 or 0x1B;
    }

    // ---- directory tree ------------------------------------------------------------------

    private void ParseDirectory(long position, string dirPath, WzDirectory dir, List<WzImageEntry> images, int depth)
    {
        if (depth > 8)
        {
            throw new InvalidDataException("directory nesting too deep");
        }

        _file.Position = position;
        int count = ReadCompressedInt();
        if (count is < 0 or > 100_000)
        {
            throw new InvalidDataException($"implausible directory entry count {count}");
        }

        var subdirs = new List<(WzDirectory Dir, uint Offset)>();
        for (int i = 0; i < count; i++)
        {
            byte type = _reader.ReadByte();
            string name;
            switch (type)
            {
                case 1: // rarely-used reference stub: int + short + offset, no name
                    _reader.ReadInt32();
                    _reader.ReadInt16();
                    ReadEncryptedOffset();
                    continue;

                case 2: // name stored back in the header's string area
                {
                    int stringOffset = _reader.ReadInt32();
                    long resume = _file.Position;
                    _file.Position = FileStart + stringOffset;
                    type = _reader.ReadByte();       // the real 3/4 type
                    name = ReadWzString();
                    _file.Position = resume;
                    break;
                }

                case 3:
                case 4:
                    name = ReadWzString();
                    break;

                default:
                    throw new InvalidDataException($"unknown directory entry type {type}");
            }

            if (name.Length == 0 || name.Any(c => c < ' ' || c > 0xFFFD))
            {
                throw new InvalidDataException("directory name failed to decode");
            }

            int size = ReadCompressedInt();
            ReadCompressedInt();                     // checksum (unused)
            uint offset = ReadEncryptedOffset();
            if (offset >= _length)
            {
                throw new InvalidDataException("entry offset beyond end of file");
            }

            if (type == 3)
            {
                var sub = new WzDirectory(name);
                dir.Entries.Add(new WzDirEntry(sub, null));
                subdirs.Add((sub, offset));
            }
            else
            {
                var image = new WzImageEntry(dirPath, name, offset, size);
                dir.Entries.Add(new WzDirEntry(null, image));
                images.Add(image);
            }
        }

        foreach ((WzDirectory sub, uint offset) in subdirs)
        {
            string child = dirPath.Length == 0 ? sub.Name : dirPath + "/" + sub.Name;
            ParseDirectory(offset, child, sub, images, depth + 1);
        }
    }

    // ---- primitive readers (shared with the image parser) --------------------------------

    internal BinaryReader Reader => _reader;

    internal int ReadCompressedInt()
    {
        sbyte b = _reader.ReadSByte();
        return b == -128 ? _reader.ReadInt32() : b;
    }

    internal long ReadCompressedLong()
    {
        sbyte b = _reader.ReadSByte();
        return b == -128 ? _reader.ReadInt64() : b;
    }

    /// <summary>Reads an encrypted 4-byte offset (position-keyed; ports <c>WzTool.GetOffset</c>).</summary>
    internal uint ReadEncryptedOffset()
    {
        uint pos = (uint)_file.Position;
        uint offset = (pos - FileStart) ^ 0xFFFFFFFF;
        unchecked
        {
            offset *= _versionHash;
            offset -= 0x581C3F6D;
        }

        int rotate = (int)(offset & 0x1F);
        offset = (offset << rotate) | (offset >> (32 - rotate));
        offset ^= _reader.ReadUInt32();
        unchecked
        {
            offset += FileStart * 2;
        }

        return offset;
    }

    /// <summary>Whether the last <see cref="ReadWzString"/> was stored as UTF-16 (else 8-bit MS932).</summary>
    internal bool LastStringWasUnicode { get; private set; }

    /// <summary>
    /// Reads an inline WZ string: negative length = 8-bit chars (mask 0xAA+i), positive =
    /// UTF-16 (mask 0xAAAA+i), both XOR'd with the keystream. 8-bit bytes decode as MS932 so
    /// Japanese text in JMS files comes out right (pure ASCII is unaffected).
    /// </summary>
    internal string ReadWzString()
    {
        sbyte small = _reader.ReadSByte();
        if (small == 0)
        {
            LastStringWasUnicode = false;
            return string.Empty;
        }

        if (small < 0)
        {
            LastStringWasUnicode = false;
            int length = small == -128 ? _reader.ReadInt32() : -small;
            if (length is < 0 or > 0x10000)
            {
                throw new InvalidDataException($"implausible ascii string length {length}");
            }

            byte[] bytes = _reader.ReadBytes(length);
            byte mask = 0xAA;
            for (int i = 0; i < length; i++)
            {
                bytes[i] = (byte)(bytes[i] ^ mask ^ _imageCrypto.KeyAt(i));
                mask++;
            }

            return Ms932.GetString(bytes);
        }
        else
        {
            LastStringWasUnicode = true;
            int length = small == 127 ? _reader.ReadInt32() : small;
            if (length is < 0 or > 0x10000)
            {
                throw new InvalidDataException($"implausible unicode string length {length}");
            }

            var chars = new char[length];
            ushort mask = 0xAAAA;
            for (int i = 0; i < length; i++)
            {
                ushort ch = _reader.ReadUInt16();
                ch ^= mask;
                ch ^= (ushort)(_imageCrypto.KeyAt(i * 2) | (_imageCrypto.KeyAt(i * 2 + 1) << 8));
                chars[i] = (char)ch;
                mask++;
            }

            return new string(chars);
        }
    }

    /// <summary>
    /// Reads a string block inside an image: 0x00/0x73 = inline here, 0x01/0x1B = stored once
    /// at <paramref name="imageStart"/>-relative offset (the dedup table).
    /// </summary>
    internal string ReadStringBlock(long imageStart)
    {
        byte flag = _reader.ReadByte();
        switch (flag)
        {
            case 0x00 or 0x73:
                return ReadWzString();

            case 0x01 or 0x1B:
            {
                int offset = _reader.ReadInt32();
                long resume = _file.Position;
                _file.Position = imageStart + offset;
                string s = ReadWzString();
                _file.Position = resume;
                return s;
            }

            default:
                throw new InvalidDataException($"unknown string block flag 0x{flag:X2}");
        }
    }

    /// <summary>
    /// Selects the string crypto for the next image parse. Pre-BB clients encrypt the images
    /// named in List.wz with a version IV while the rest are plain, so a single archive can mix
    /// both — the dumper retries an image with each candidate until its root parses.
    /// </summary>
    internal void UseImageCrypto(WzCrypto? crypto) => _imageCrypto = crypto ?? _crypto;

    /// <summary>The crypto the current image parse is using.</summary>
    internal WzCrypto CurrentImageCrypto => _imageCrypto;

    /// <summary>Candidate cryptos for encrypted images, lazily built once per archive.</summary>
    internal IReadOnlyList<WzCrypto> ImageCryptoCandidates => _cryptoCandidates ??=
        WzCrypto.KnownIvs.Select(k => new WzCrypto(k.Iv)).ToList();

    private List<WzCrypto>? _cryptoCandidates;

    internal static readonly Encoding Ms932 = CodePagesEncodingProvider.Instance.GetEncoding(932)
        ?? Encoding.ASCII;
}
