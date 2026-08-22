using System.Buffers.Binary;
using System.Text;
using Cronus.Common;

namespace Cronus.Network.Packets;

/// <summary>
/// Little-endian inbound packet reader (ports <c>tacos.packet.ClientPacket</c> / CInPacket).
/// Reads the opcode header then fields. Strings are length-prefixed (<c>[len:2][bytes]</c>)
/// in the configured code page (MS932 / Shift-JIS for JMS).
/// </summary>
public sealed class PacketReader
{
    /// <summary>Default wire encoding for strings. JMS = MS932 (Shift-JIS).</summary>
    public static Encoding DefaultEncoding { get; set; } = CodePage.Get("shift_jis");

    private readonly byte[] _buffer;
    private readonly Encoding _encoding;
    private int _position;

    public PacketReader(byte[] buffer, Encoding? encoding = null)
    {
        _buffer = buffer;
        _encoding = encoding ?? DefaultEncoding;
    }

    /// <summary>Bytes not yet consumed.</summary>
    public int Remaining => _buffer.Length - _position;

    /// <summary>Current read offset.</summary>
    public int Position => _position;

    /// <summary>Total length of the underlying packet.</summary>
    public int Length => _buffer.Length;

    /// <summary>Hex dump of the whole packet, position untouched — wire-diagnostics logging.</summary>
    public string ToHex() => Convert.ToHexString(_buffer);

    /// <summary>
    /// Reads the opcode header (1 or 2 bytes) and returns its numeric value.
    /// </summary>
    public int ReadHeader(int headerSize = 2)
        => headerSize == 2 ? ReadShort() & 0xFFFF : ReadByte();

    public byte ReadByte()
    {
        EnsureAvailable(1);
        return _buffer[_position++];
    }

    public bool ReadBool() => ReadByte() != 0;

    public short ReadShort()
    {
        EnsureAvailable(2);
        short value = BinaryPrimitives.ReadInt16LittleEndian(_buffer.AsSpan(_position));
        _position += 2;
        return value;
    }

    public int ReadInt()
    {
        EnsureAvailable(4);
        int value = BinaryPrimitives.ReadInt32LittleEndian(_buffer.AsSpan(_position));
        _position += 4;
        return value;
    }

    public long ReadLong()
    {
        EnsureAvailable(8);
        long value = BinaryPrimitives.ReadInt64LittleEndian(_buffer.AsSpan(_position));
        _position += 8;
        return value;
    }

    public double ReadDouble() => BitConverter.Int64BitsToDouble(ReadLong());

    public byte[] ReadBytes(int count)
    {
        EnsureAvailable(count);
        byte[] result = _buffer.AsSpan(_position, count).ToArray();
        _position += count;
        return result;
    }

    /// <summary>Reads a length-prefixed string: <c>[len:2(LE)][encoded bytes]</c>.</summary>
    public string ReadString()
    {
        int length = ReadShort() & 0xFFFF;
        EnsureAvailable(length);
        string value = _encoding.GetString(_buffer, _position, length);
        _position += length;
        return value;
    }

    /// <summary>Reads a fixed-size buffer and decodes it as a string, trimming trailing NULs.</summary>
    public string ReadFixedString(int size)
    {
        EnsureAvailable(size);
        int end = _position;
        int limit = _position + size;
        while (end < limit && _buffer[end] != 0)
        {
            end++;
        }

        string value = _encoding.GetString(_buffer, _position, end - _position);
        _position += size;
        return value;
    }

    /// <summary>Reads all remaining bytes.</summary>
    public byte[] ReadRemaining()
    {
        byte[] result = _buffer.AsSpan(_position).ToArray();
        _position = _buffer.Length;
        return result;
    }

    /// <summary>Skips <paramref name="count"/> bytes.</summary>
    public void Skip(int count)
    {
        EnsureAvailable(count);
        _position += count;
    }

    private void EnsureAvailable(int count)
    {
        if (_position + count > _buffer.Length)
        {
            throw new EndOfStreamException(
                $"Attempted to read {count} bytes at offset {_position} of a {_buffer.Length}-byte packet.");
        }
    }
}
