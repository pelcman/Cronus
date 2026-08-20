using Cronus.Network.Packets;

namespace Cronus.Network;

/// <summary>
/// Receives connection lifecycle and decoded-packet callbacks for a <see cref="MapleSession"/>.
/// Implementations dispatch on the numeric opcode (resolve names via <see cref="OpcodeTable"/>).
/// </summary>
public interface IPacketHandler
{
    /// <summary>Called once after the handshake completes and encryption is synced.</summary>
    ValueTask OnConnectedAsync(MapleSession session);

    /// <summary>Called for each decoded, decrypted inbound packet (opcode header already read).</summary>
    ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet);

    /// <summary>Called once when the session ends (graceful close or error).</summary>
    ValueTask OnDisconnectedAsync(MapleSession session, Exception? error);
}

/// <summary>No-op base class so handlers can override only what they need.</summary>
public abstract class PacketHandlerBase : IPacketHandler
{
    public virtual ValueTask OnConnectedAsync(MapleSession session) => ValueTask.CompletedTask;

    public virtual ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
        => ValueTask.CompletedTask;

    public virtual ValueTask OnDisconnectedAsync(MapleSession session, Exception? error)
        => ValueTask.CompletedTask;
}
