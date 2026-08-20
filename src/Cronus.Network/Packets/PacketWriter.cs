using System.Buffers.Binary;
using System.Text;
using Cronus.Common;

namespace Cronus.Network.Packets;

/// <summary>
/// Little-endian outbound packet builder (ports <c>tacos.packet.ServerPacket</c> /
/// COutPacket). Writes an opcode header followed by fields. All multi-byte integers are
/// little-endian; strings are length-prefixed (<c>[len:2][bytes]</c>) in the configured
/// code page (MS932 / Shift-JIS for JMS).
/// </summary>
public sealed class PacketWriter
{
    /// <summary>
    /// Default wire encoding for strings. JMS = MS932 (Shift-JIS). Registering the
    /// code-pages provider happens inside <see cref="CodePage.Get"/>.
    /// </summary>
    public static Encoding DefaultEncoding { get; set; } = CodePage.Get("shift_jis");

    private readonly Encoding _encoding;
    private byte[] _buffer;
    private int _length;

    public PacketWriter(int capacity = 64, Encoding? encoding = null)
    {
        _buffer = new byte[capacity < 4 ? 4 : capacity];
        _encoding = encoding ?? DefaultEncoding;
    }

    /// <summary>
    /// Creates a writer whose first bytes are the opcode header.
    /// </summary>
    /// <param name="opcode">Resolved numeric opcode value.</param>
    /// <param name="headerSize">Header width in bytes (JMS = 2).</param>
    public PacketWriter(int opcode, int headerSize = 2, Encoding? encoding = null)
        : this(64, encoding)
    {
        WriteByte((byte)(opcode & 0xFF));
        if (headerSize == 2)
        {
            WriteByte((byte)((opcode >> 8) & 0xFF));
        }
    }

    /// <summary>Number of bytes written so far.</summary>
    public int Length => _length;

    public void WriteByte(byte value)
    {
        EnsureCapacity(1);
        _buffer[_length++] = value;
    }

    public void WriteByte(int value) => WriteByte((byte)value);

    public void WriteBool(bool value) => WriteByte(value ? (byte)1 : (byte)0);

    public void WriteShort(short value)
    {
        EnsureCapacity(2);
        BinaryPrimitives.WriteInt16LittleEndian(_buffer.AsSpan(_length), value);
        _length += 2;
    }

    public void WriteShort(int value) => WriteShort((short)value);

    public void WriteInt(int value)
    {
        EnsureCapacity(4);
        BinaryPrimitives.WriteInt32LittleEndian(_buffer.AsSpan(_length), value);
        _length += 4;
    }

    public void WriteLong(long value)
    {
        EnsureCapacity(8);
        BinaryPrimitives.WriteInt64LittleEndian(_buffer.AsSpan(_length), value);
        _length += 8;
    }

    public void WriteDouble(double value) => WriteLong(BitConverter.DoubleToInt64Bits(value));

    /// <summary>Writes a length-prefixed string: <c>[len:2(LE)][encoded bytes]</c>.</summary>
    public void WriteString(string value)
    {
        byte[] bytes = _encoding.GetBytes(value);
        WriteShort((short)bytes.Length);
        WriteBytes(bytes);
    }

    /// <summary>
    /// Writes a fixed-size string field: encoded bytes then zero-padding up to
    /// <paramref name="size"/>. Does not write a length prefix.
    /// </summary>
    public void WriteFixedString(string value, int size)
    {
        byte[] bytes = _encoding.GetBytes(value);
        int copy = Math.Min(bytes.Length, size);
        WriteBytes(bytes.AsSpan(0, copy));
        WriteZero(size - copy);
    }

    public void WriteBytes(ReadOnlySpan<byte> bytes)
    {
        EnsureCapacity(bytes.Length);
        bytes.CopyTo(_buffer.AsSpan(_length));
        _length += bytes.Length;
    }

    public void WriteZero(int count)
    {
        if (count <= 0)
        {
            return;
        }

        EnsureCapacity(count);
        _buffer.AsSpan(_length, count).Clear();
        _length += count;
    }

    /// <summary>Read-only view of the bytes written so far.</summary>
    public ReadOnlySpan<byte> AsSpan() => _buffer.AsSpan(0, _length);

    public byte[] ToArray() => _buffer.AsSpan(0, _length).ToArray();

    private void EnsureCapacity(int extra)
    {
        int required = _length + extra;
        if (required <= _buffer.Length)
        {
            return;
        }

        int newSize = _buffer.Length * 2;
        while (newSize < required)
        {
            newSize *= 2;
        }

        Array.Resize(ref _buffer, newSize);
    }
}
