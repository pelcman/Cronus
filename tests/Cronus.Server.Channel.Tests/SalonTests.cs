using Cronus.Common;
using Cronus.Network;
using Cronus.Network.Packets;
using Cronus.Server.Channel;
using Xunit;

namespace Cronus.Server.Channel.Tests;

/// <summary>The style-picker dialog packet used by salon (hair/face/skin) NPC scripts.</summary>
public class SalonTests
{
    private static OpcodeTable ServerOps { get; } =
        OpcodeTable.LoadFile(Path.Combine(AppContext.BaseDirectory, "opcodes", "JMS_v186_ServerPacket.properties"));

    [Fact]
    public void ScriptMessageAvatar_HasExactAskAvatarLayout()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        var r = new PacketReader(
            packets.ScriptMessageAvatar(1012103, "どの髪型?", new[] { 30030, 30031, 30032 }),
            ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.ScriptMessage), r.ReadHeader());
        Assert.Equal(4, r.ReadByte());          // nSpeakerTypeID
        Assert.Equal(1012103, r.ReadInt());     // nSpeakerTemplateID
        Assert.Equal(8, r.ReadByte());          // nMsgType = SM_ASKAVATAR
        Assert.Equal(0, r.ReadByte());          // param (JMS >= 180)
        Assert.Equal("どの髪型?", r.ReadString());
        Assert.Equal(3, r.ReadByte());          // candidate count
        Assert.Equal(30030, r.ReadInt());
        Assert.Equal(30031, r.ReadInt());
        Assert.Equal(30032, r.ReadInt());
        Assert.Equal(0, r.Remaining);           // no CMS trailing int for JMS
    }

    [Fact]
    public void RpsPackets_HaveExactVanillaLayouts()
    {
        var packets = new ChannelPackets(ServerOps, ServerConfig.Jms186);

        var open = new PacketReader(packets.RpsOpen(9000019), ServerConfig.Jms186.CodePage);
        Assert.Equal(ServerOps.Get(ServerOpcode.RpsGame), open.ReadHeader());
        Assert.Equal(ChannelPackets.RpsOpenType, open.ReadByte());
        Assert.Equal(9000019, open.ReadInt());
        Assert.Equal(0, open.Remaining);

        var pick = new PacketReader(packets.RpsSelection(2, -1), ServerConfig.Jms186.CodePage);
        pick.ReadHeader();
        Assert.Equal(ChannelPackets.RpsNpcSelection, pick.ReadByte());
        Assert.Equal(2, pick.ReadByte());
        Assert.Equal(-1, (sbyte)pick.ReadByte()); // negative streak = lost
        Assert.Equal(0, pick.Remaining);

        var start = new PacketReader(packets.RpsResult(ChannelPackets.RpsStartGame), ServerConfig.Jms186.CodePage);
        start.ReadHeader();
        Assert.Equal(ChannelPackets.RpsStartGame, start.ReadByte());
        Assert.Equal(0, start.Remaining);
    }
}
