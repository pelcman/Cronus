using System.Buffers.Binary;
using Cronus.Common;
using Cronus.Network;
using Cronus.Network.Packets;
using Xunit;

namespace Cronus.Network.Tests;

public class HandshakeTests
{
    [Fact]
    public void BuildHello_HasExpectedStructure()
    {
        byte[] recvIv = { 70, 114, 122, 0x11 };
        byte[] sendIv = { 82, 48, 120, 0x22 };

        byte[] hello = Handshake.BuildHello(ServerConfig.Jms186, recvIv, sendIv);

        // Leading 2 bytes = body size (everything after them).
        int bodySize = BinaryPrimitives.ReadUInt16LittleEndian(hello);
        Assert.Equal(hello.Length - 2, bodySize);

        // Parse the body back.
        var reader = new PacketReader(hello, ServerConfig.Jms186.CodePage);
        reader.Skip(2); // size prefix
        Assert.Equal(186, reader.ReadShort());
        Assert.Equal("0", reader.ReadString()); // sub-version
        Assert.Equal(recvIv, reader.ReadBytes(4));
        Assert.Equal(sendIv, reader.ReadBytes(4));
        Assert.Equal((byte)Region.Jms, reader.ReadByte());
        Assert.Equal(3, (int)Region.Jms);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void BuildHello_RejectsWrongLengthIvs()
    {
        Assert.Throws<ArgumentException>(
            () => Handshake.BuildHello(ServerConfig.Jms186, new byte[3], new byte[4]));
    }
}
