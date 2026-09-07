using System.Text;

namespace Cronus.Data.Wz;

/// <summary>
/// Serializes a <see cref="WzNode"/> tree back into .img bytes (the inverse of
/// <see cref="WzImageReader"/>): strings encoded with the target archive's keystream and deduplicated
/// through the image-relative offset table, extended blocks size-prefixed, canvas and sound payloads
/// written verbatim. Feeding a freshly parsed tree back in reproduces the original image.
/// </summary>
public sealed class WzImageWriter
{
    private readonly WzCrypto _crypto;
    private readonly MemoryStream _stream = new();
    private readonly BinaryWriter _w;
    private readonly Dictionary<string, int> _stringOffsets = new(StringComparer.Ordinal);

    private WzImageWriter(WzCrypto crypto)
    {
        _crypto = crypto;
        _w = new BinaryWriter(_stream);
    }

    /// <summary>Serializes <paramref name="root"/> (a Property container) with <paramref name="crypto"/>.</summary>
    public static byte[] Serialize(WzNode root, WzCrypto crypto)
    {
        if (root.Kind != WzNodeKind.Property)
        {
            throw new ArgumentException("an image root must be a Property container", nameof(root));
        }

        var writer = new WzImageWriter(crypto);
        writer.WriteStringBlock("Property", inline: 0x73, reference: 0x1B, unicode: false);
        writer._w.Write((ushort)0);
        writer.WritePropertyList(root.Children);
        return writer._stream.ToArray();
    }

    private void WritePropertyList(List<WzNode> nodes)
    {
        WriteCompressedInt(nodes.Count);
        foreach (WzNode node in nodes)
        {
            WriteStringBlock(node.Name, inline: 0x00, reference: 0x01, node.NameIsUnicode);
            switch (node.Kind)
            {
                case WzNodeKind.Null:
                    _w.Write((byte)0);
                    break;

                case WzNodeKind.Short:
                    _w.Write(node.TypeCode is 2 or 11 ? node.TypeCode : (byte)2);
                    _w.Write((short)node.IntValue);
                    break;

                case WzNodeKind.Int:
                    _w.Write(node.TypeCode is 3 or 19 ? node.TypeCode : (byte)3);
                    WriteCompressedInt((int)node.IntValue);
                    break;

                case WzNodeKind.Long:
                    _w.Write((byte)20);
                    WriteCompressedLong(node.IntValue);
                    break;

                case WzNodeKind.Float:
                    _w.Write((byte)4);
                    if (node.FloatValue == 0)
                    {
                        _w.Write((byte)0);
                    }
                    else
                    {
                        _w.Write((byte)0x80);
                        _w.Write((float)node.FloatValue);
                    }

                    break;

                case WzNodeKind.Double:
                    _w.Write((byte)5);
                    _w.Write(node.FloatValue);
                    break;

                case WzNodeKind.String:
                    _w.Write((byte)8);
                    WriteStringBlock(node.StringValue, inline: 0x00, reference: 0x01, node.StringIsUnicode);
                    break;

                default:
                {
                    _w.Write((byte)9);
                    long sizeAt = _stream.Position;
                    _w.Write((uint)0);
                    long start = _stream.Position;
                    WriteExtended(node);
                    long end = _stream.Position;
                    _stream.Position = sizeAt;
                    _w.Write((uint)(end - start));
                    _stream.Position = end;
                    break;
                }
            }
        }
    }

    private void WriteExtended(WzNode node)
    {
        switch (node.Kind)
        {
            case WzNodeKind.Property:
                WriteStringBlock("Property", 0x73, 0x1B, unicode: false);
                _w.Write((ushort)0);
                WritePropertyList(node.Children);
                break;

            case WzNodeKind.Canvas:
                WriteStringBlock("Canvas", 0x73, 0x1B, unicode: false);
                _w.Write((byte)0);
                _w.Write((byte)(node.Children.Count > 0 ? 1 : 0));
                if (node.Children.Count > 0)
                {
                    _w.Write((ushort)0);
                    WritePropertyList(node.Children);
                }

                WriteCompressedInt(node.Width);
                WriteCompressedInt(node.Height);
                WriteCompressedInt(node.Format);
                _w.Write(node.Format2);
                _w.Write((uint)0);
                _w.Write(node.CanvasData.Length + 1);
                _w.Write((byte)0);
                _w.Write(node.CanvasData);
                break;

            case WzNodeKind.Vector:
                WriteStringBlock("Shape2D#Vector2D", 0x73, 0x1B, unicode: false);
                WriteCompressedInt(node.X);
                WriteCompressedInt(node.Y);
                break;

            case WzNodeKind.Convex:
                WriteStringBlock("Shape2D#Convex2D", 0x73, 0x1B, unicode: false);
                WriteCompressedInt(node.Children.Count);
                foreach (WzNode point in node.Children)
                {
                    WriteExtended(point);
                }

                break;

            case WzNodeKind.Uol:
                WriteStringBlock("UOL", 0x73, 0x1B, unicode: false);
                _w.Write((byte)0);
                WriteStringBlock(node.StringValue, 0x00, 0x01, node.StringIsUnicode);
                break;

            case WzNodeKind.Sound:
                WriteStringBlock("Sound_DX8", 0x73, 0x1B, unicode: false);
                _w.Write(node.RawPayload);
                break;

            default:
                throw new InvalidDataException($"{node.Kind} is not an extended property");
        }
    }

    // ---- primitives ------------------------------------------------------------------------

    /// <summary>Writes a string block: the first occurrence inline (<paramref name="inline"/> flag +
    /// string), later ones as <paramref name="reference"/> flag + the offset of that first copy.</summary>
    private void WriteStringBlock(string s, byte inline, byte reference, bool unicode)
    {
        if (_stringOffsets.TryGetValue(s, out int offset))
        {
            _w.Write(reference);
            _w.Write(offset);
            return;
        }

        _w.Write(inline);
        _stringOffsets[s] = (int)_stream.Position;
        WriteWzString(_w, s, _crypto, unicode);
    }

    /// <summary>Writes a WZ string: 8-bit MS932 with the 0xAA+i mask unless it was (or must be)
    /// UTF-16 with the 0xAAAA+i mask; both XOR'd with the keystream.</summary>
    internal static void WriteWzString(BinaryWriter w, string s, WzCrypto crypto, bool unicode)
    {
        if (s.Length == 0)
        {
            w.Write((sbyte)0);
            return;
        }

        byte[]? bytes = unicode ? null : TryEncodeMs932(s);
        if (bytes is not null)
        {
            if (bytes.Length < 128)
            {
                w.Write((sbyte)(-bytes.Length));
            }
            else
            {
                w.Write((sbyte)-128);
                w.Write(bytes.Length);
            }

            byte mask = 0xAA;
            for (int i = 0; i < bytes.Length; i++)
            {
                w.Write((byte)(bytes[i] ^ mask ^ crypto.KeyAt(i)));
                mask++;
            }

            return;
        }

        if (s.Length < 127)
        {
            w.Write((sbyte)s.Length);
        }
        else
        {
            w.Write((sbyte)127);
            w.Write(s.Length);
        }

        ushort umask = 0xAAAA;
        for (int i = 0; i < s.Length; i++)
        {
            ushort ch = s[i];
            ch ^= umask;
            ch ^= (ushort)(crypto.KeyAt(i * 2) | (crypto.KeyAt(i * 2 + 1) << 8));
            w.Write(ch);
            umask++;
        }
    }

    /// <summary>The encoded length of a WZ string, for layout arithmetic.</summary>
    internal static int WzStringLength(string s, bool unicode)
    {
        if (s.Length == 0)
        {
            return 1;
        }

        byte[]? bytes = unicode ? null : TryEncodeMs932(s);
        if (bytes is not null)
        {
            return (bytes.Length < 128 ? 1 : 5) + bytes.Length;
        }

        return (s.Length < 127 ? 1 : 5) + s.Length * 2;
    }

    private static readonly Encoding StrictMs932 = StrictMs932Encoding();

    /// <summary>MS932 that throws instead of substituting '?', so unrepresentable text falls back to UTF-16.</summary>
    private static Encoding StrictMs932Encoding()
    {
        var e = (Encoding)(CodePagesEncodingProvider.Instance.GetEncoding(932) ?? Encoding.ASCII).Clone();
        e.EncoderFallback = EncoderFallback.ExceptionFallback;
        e.DecoderFallback = DecoderFallback.ExceptionFallback;
        return e;
    }

    /// <summary>MS932 bytes when every char is representable and round-trips, else null.</summary>
    private static byte[]? TryEncodeMs932(string s)
    {
        try
        {
            byte[] bytes = StrictMs932.GetBytes(s);
            return StrictMs932.GetString(bytes) == s ? bytes : null;
        }
        catch (EncoderFallbackException)
        {
            return null;
        }
    }

    private void WriteCompressedInt(int value) => WriteCompressedInt(_w, value);

    internal static void WriteCompressedInt(BinaryWriter w, int value)
    {
        if (value is >= -127 and <= 127)
        {
            w.Write((sbyte)value);
        }
        else
        {
            w.Write((sbyte)-128);
            w.Write(value);
        }
    }

    internal static int CompressedIntLength(int value) => value is >= -127 and <= 127 ? 1 : 5;

    private void WriteCompressedLong(long value)
    {
        if (value is >= -127 and <= 127)
        {
            _w.Write((sbyte)value);
        }
        else
        {
            _w.Write((sbyte)-128);
            _w.Write(value);
        }
    }
}
