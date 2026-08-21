using Cronus.Common;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

public class EffectTests
{
    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void UserEffectRemote_LevelUp_HasCharIdThenType()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        byte[] bytes = packets.UserEffectRemote(characterId: 77, effectType: ChannelPackets.UserEffectLevelUp);

        var r = new PacketReader(bytes, ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.UserEffectRemote), r.ReadHeader());
        Assert.Equal(77, r.ReadInt());   // whose effect
        Assert.Equal(0, r.ReadByte());   // UserEffect_LevelUp — no extra payload
        Assert.Equal(0, r.Remaining);
    }
}
