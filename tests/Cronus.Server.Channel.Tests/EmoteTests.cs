using Cronus.Common;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class EmoteTests
{
    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void UserEmotion_HasExactLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        byte[] bytes = packets.UserEmotion(characterId: 42, expression: 3);

        var reader = new PacketReader(bytes, ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.UserEmotion), reader.ReadHeader());
        Assert.Equal(42, reader.ReadInt());  // remote character id
        Assert.Equal(3, reader.ReadInt());   // expression
        Assert.Equal(-1, reader.ReadInt());  // duration (DataCUser.Emotion)
        Assert.Equal(0, reader.ReadByte());
        Assert.Equal(0, reader.Remaining);
    }
}
