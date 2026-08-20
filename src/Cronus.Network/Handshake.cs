using System.Buffers.Binary;
using System.Globalization;
using Cronus.Common;
using Cronus.Network.Packets;

namespace Cronus.Network;

/// <summary>
/// Builds the plaintext Hello packet sent immediately after a connection opens, before
/// encryption begins (ports <c>ResCClientSocket.getHello</c>, JMS branch). Wire layout:
/// <code>[bodySize:2 LE] [version:2 LE] [subVersion:string] [recvIv:4] [sendIv:4] [region:1]</code>
/// where <c>bodySize</c> counts everything after the leading 2 bytes.
/// </summary>
public static class Handshake
{
    public static byte[] BuildHello(ServerConfig config, ReadOnlySpan<byte> recvIv, ReadOnlySpan<byte> sendIv)
    {
        if (recvIv.Length != 4 || sendIv.Length != 4)
        {
            throw new ArgumentException("IVs must be exactly 4 bytes.");
        }

        var body = new PacketWriter(32, config.CodePage);
        body.WriteShort((short)config.Version);
        body.WriteString(config.SubVersion.ToString(CultureInfo.InvariantCulture));
        body.WriteBytes(recvIv);
        body.WriteBytes(sendIv);
        body.WriteByte(config.Region.Code());

        ReadOnlySpan<byte> bodyBytes = body.AsSpan();
        var packet = new byte[2 + bodyBytes.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(packet, (ushort)bodyBytes.Length);
        bodyBytes.CopyTo(packet.AsSpan(2));
        return packet;
    }
}
