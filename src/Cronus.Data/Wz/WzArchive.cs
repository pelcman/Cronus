using System.Text;

namespace Cronus.Data.Wz;

/// <summary>One .img inside an archive: its directory path (e.g. "Map/Map1"), name, and offset.</summary>
public sealed record WzImageEntry(string Directory, string Name, uint Offset, int Size)
{
    /// <summary>The dump-style relative path: directory + name + ".xml".</summary>
    public string RelativePath => Directory.Length == 0 ? Name + ".xml" : Directory + "/" + Name + ".xml";
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
    }

    /// <summary>The archive's base name ("Map" for Map.wz) — the first path segment in dumps.</summary>
    public string BaseName { get; }

    /// <summary>Where the data region starts (header size); offsets are relative to this.</summary>
    public uint FileStart { get; private set; }

    /// <summary>The detected game version (e.g. 186) — informational once parsing succeeds.</summary>
    public int Version { get; private set; }

    /// <summary>The name of the IV that decoded the archive ("none" / "gms" / "ems").</summary>
    public string IvName { get; private set; } = "";

    /// <summary>Every image in the archive, in directory order.</summary>
    public IReadOnlyList<WzImageEntry> Images { get; private set; } = Array.Empty<WzImageEntry>();

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

    // ---- header + (iv, version) detection ------------------------------------------------

    private void Parse()
    {
        _file.Position = 0;
        if (_reader.ReadUInt32() != 0x31474B50) // "PKG1"
        {
            throw new InvalidDataException("not a WZ archive (missing PKG1 magic)");
        }

        _reader.ReadUInt64();                   // file size
        FileStart = _reader.ReadUInt32();

        _file.Position = FileStart;
        int encVer = _reader.ReadUInt16();

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
                    ParseDirectory(FileStart + 2, "", images, depth: 0);
                    if (images.Count == 0 || !LooksLikeImage(images[0]))
                    {
                        continue;
                    }

                    Version = version;
                    IvName = ivName;
                    Images = images;
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

    private static (byte EncVer, uint Hash) HashVersion(int version)
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

    private void ParseDirectory(long position, string dirPath, List<WzImageEntry> images, int depth)
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

        var subdirs = new List<(string Name, uint Offset)>();
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
                subdirs.Add((name, offset));
            }
            else
            {
                images.Add(new WzImageEntry(dirPath, name, offset, size));
            }
        }

        foreach ((string name, uint offset) in subdirs)
        {
            string child = dirPath.Length == 0 ? name : dirPath + "/" + name;
            ParseDirectory(offset, child, images, depth + 1);
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
            return string.Empty;
        }

        if (small < 0)
        {
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

    /// <summary>Candidate cryptos for encrypted images, lazily built once per archive.</summary>
    internal IReadOnlyList<WzCrypto> ImageCryptoCandidates => _cryptoCandidates ??=
        WzCrypto.KnownIvs.Select(k => new WzCrypto(k.Iv)).ToList();

    private List<WzCrypto>? _cryptoCandidates;

    private static readonly Encoding Ms932 = CodePagesEncodingProvider.Instance.GetEncoding(932)
        ?? Encoding.ASCII;
}
