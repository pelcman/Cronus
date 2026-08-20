using Cronus.Network;
using Cronus.Network.Packets;

namespace Cronus.Server.Host;

/// <summary>
/// Decorates an <see cref="IPacketHandler"/> with console lifecycle/packet logging.
/// Kept dependency-light for now; a structured logger (Serilog) is on the backlog.
/// </summary>
public sealed class LoggingHandler : IPacketHandler
{
    private readonly IPacketHandler _inner;
    private readonly string _tag;

    public LoggingHandler(IPacketHandler inner, string tag)
    {
        _inner = inner;
        _tag = tag;
    }

    public ValueTask OnConnectedAsync(MapleSession session)
    {
        Log("connected");
        return _inner.OnConnectedAsync(session);
    }

    public ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
    {
        Log($"recv opcode 0x{opcode:X4} ({packet.Length} bytes)");
        return _inner.OnPacketAsync(session, opcode, packet);
    }

    public ValueTask OnDisconnectedAsync(MapleSession session, Exception? error)
    {
        Log(error is null ? "disconnected" : $"disconnected: {error.Message}");
        return _inner.OnDisconnectedAsync(session, error);
    }

    private void Log(string message)
        => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{_tag}] {message}");
}
