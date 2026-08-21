using Cronus.Common;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class SitTests
{
    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void UserSitResult_Sitting_WritesSeatId()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        byte[] bytes = packets.UserSitResult(seatId: 5);

        var reader = new PacketReader(bytes, ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.UserSitResult), reader.ReadHeader());
        Assert.True(reader.ReadBool());       // sitting
        Assert.Equal(5, reader.ReadShort());  // seat id
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void UserSitResult_Standing_JustTheFlag()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        byte[] bytes = packets.UserSitResult(seatId: -1);

        var reader = new PacketReader(bytes, ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.UserSitResult), reader.ReadHeader());
        Assert.False(reader.ReadBool());      // standing — no seat id follows
        Assert.Equal(0, reader.Remaining);
    }
}
