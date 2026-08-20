using Cronus.Common;
using Cronus.Network.Packets;
using Xunit;

namespace Cronus.Network.Tests;

public class PacketRoundTripTests
{
    [Fact]
    public void PrimitivesRoundTrip()
    {
        var writer = new PacketWriter();
        writer.WriteByte(0x7F);
        writer.WriteBool(true);
        writer.WriteShort(-12345);
        writer.WriteInt(0x1234_5678);
        writer.WriteLong(0x0123_4567_89AB_CDEF);
        writer.WriteBytes(new byte[] { 1, 2, 3, 4, 5 });

        var reader = new PacketReader(writer.ToArray());
        Assert.Equal(0x7F, reader.ReadByte());
        Assert.True(reader.ReadBool());
        Assert.Equal(-12345, reader.ReadShort());
        Assert.Equal(0x1234_5678, reader.ReadInt());
        Assert.Equal(0x0123_4567_89AB_CDEF, reader.ReadLong());
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, reader.ReadBytes(5));
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void LittleEndianByteOrder()
    {
        var writer = new PacketWriter();
        writer.WriteInt(0x11223344);
        Assert.Equal(new byte[] { 0x44, 0x33, 0x22, 0x11 }, writer.ToArray());
    }

    [Fact]
    public void ShiftJisStringRoundTrips()
    {
        var encoding = CodePage.Get("shift_jis");
        var writer = new PacketWriter(encoding: encoding);
        writer.WriteString("テストID");

        var reader = new PacketReader(writer.ToArray(), encoding);
        Assert.Equal("テストID", reader.ReadString());
    }

    [Fact]
    public void OpcodeHeaderIsWrittenLittleEndian()
    {
        var writer = new PacketWriter(opcode: 0x001D, headerSize: 2);
        byte[] bytes = writer.ToArray();
        Assert.Equal(0x1D, bytes[0]);
        Assert.Equal(0x00, bytes[1]);

        var reader = new PacketReader(bytes);
        Assert.Equal(0x001D, reader.ReadHeader());
    }

    [Fact]
    public void FixedStringPadsWithZeros()
    {
        var writer = new PacketWriter();
        writer.WriteFixedString("AB", 5);
        byte[] bytes = writer.ToArray();
        Assert.Equal(new byte[] { (byte)'A', (byte)'B', 0, 0, 0 }, bytes);

        var reader = new PacketReader(bytes);
        Assert.Equal("AB", reader.ReadFixedString(5));
    }

    [Fact]
    public void ReadingPastEndThrows()
    {
        var reader = new PacketReader(new byte[] { 1, 2 });
        reader.ReadShort();
        Assert.Throws<EndOfStreamException>(() => reader.ReadByte());
    }
}
