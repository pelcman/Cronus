using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Cronus.Common;
using Cronus.Network;
using Cronus.Network.Packets;

namespace Cronus.Debug.Bot;

/// <summary>
/// One protocol-level bot connection: a real encrypted JMS v186 client session over TCP
/// (handshake, AES-OFB, framing — the same wire path a live client uses). Scenarios drive it
/// by sending client packets and awaiting expected server opcodes.
/// </summary>
public sealed class BotClient : PacketHandlerBase, IAsyncDisposable
{
    private sealed record Pending(int Opcode, TaskCompletionSource<byte[]> Tcs, Func<PacketReader, bool>? Match);

    private readonly ServerConfig _config;
    private readonly List<Pending> _waiters = new();
    private readonly ConcurrentQueue<(int Opcode, byte[] Packet)> _unclaimed = new();
    private readonly object _gate = new();

    private TcpClient? _tcp;
    private MapleSession? _session;
    private Task? _runTask;
    private CancellationTokenSource? _cts;
    private TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public BotClient(string name, ServerConfig config, OpcodeTable clientOps, OpcodeTable serverOps)
    {
        Name = name;
        _config = config;
        ClientOps = clientOps;
        ServerOps = serverOps;
    }

    public string Name { get; }

    public OpcodeTable ClientOps { get; }

    public OpcodeTable ServerOps { get; }

    /// <summary>Set once the login flow learns it; used by the channel scenarios.</summary>
    public int CharacterId { get; set; }

    /// <summary>The character's display name ("Bot1"…), set by the login flow.</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Opens the encrypted session to <paramref name="endpoint"/>.</summary>
    public async Task ConnectAsync(IPEndPoint endpoint, CancellationToken ct)
    {
        await DisconnectAsync().ConfigureAwait(false);

        _tcp = new TcpClient();
        await _tcp.ConnectAsync(endpoint, ct).ConfigureAwait(false);
        NetworkStream stream = _tcp.GetStream();

        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _session = new MapleSession(
            PipeReader.Create(stream), PipeWriter.Create(stream), _config, SessionRole.Client, this);
        _runTask = _session.RunAsync(_cts.Token);

        // OnConnectedAsync fires once the Hello handshake has synced the crypto — only then
        // is it safe to send the first packet.
        await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);
    }

    public override ValueTask OnConnectedAsync(MapleSession session)
    {
        _ready.TrySetResult();
        return ValueTask.CompletedTask;
    }

    /// <summary>Closes the current connection (migrations open a fresh one).</summary>
    public async Task DisconnectAsync()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // already torn down
        }

        if (_session is not null)
        {
            try
            {
                await _session.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // the socket may already be gone; the disconnect is what matters
            }
        }

        _tcp?.Close();
        _session = null;
        _tcp = null;
        _runTask = null;
        _unclaimed.Clear();
        lock (_gate)
        {
            foreach (Pending p in _waiters)
            {
                p.Tcs.TrySetCanceled();
            }

            _waiters.Clear();
        }
    }

    public PacketWriter NewPacket(string clientOpcode)
        => new(ClientOps.Get(clientOpcode), _config.PacketHeaderSize, _config.CodePage);

    public ValueTask SendAsync(PacketWriter w) => _session!.SendAsync(w.ToArray());

    /// <summary>
    /// Waits for the next server packet with the given opcode (already-received unclaimed
    /// packets match first). The returned reader is positioned after the opcode header.
    /// </summary>
    public Task<PacketReader> ExpectAsync(string serverOpcode, TimeSpan? timeout = null)
        => ExpectAsync(serverOpcode, null, timeout);

    /// <summary>
    /// Awaits the next server packet with <paramref name="serverOpcode"/> that also satisfies
    /// <paramref name="match"/>. The predicate gets a fresh reader positioned at the body; packets
    /// that match the opcode but not the predicate stay buffered for other waiters. This lets a bot
    /// pick out its own drop/leave events when several bots share a field.
    /// </summary>
    public async Task<PacketReader> ExpectAsync(string serverOpcode, Func<PacketReader, bool>? match, TimeSpan? timeout = null)
    {
        int opcode = ServerOps.Get(serverOpcode);
        TaskCompletionSource<byte[]> tcs;
        lock (_gate)
        {
            // Drain anything already buffered; requeue non-matches in arrival order.
            int n = _unclaimed.Count;
            for (int i = 0; i < n; i++)
            {
                if (!_unclaimed.TryDequeue(out (int Opcode, byte[] Packet) buffered))
                {
                    break;
                }

                if (buffered.Opcode == opcode && (match is null || match(ReaderFor(buffered.Packet))))
                {
                    return ReaderFor(buffered.Packet);
                }

                _unclaimed.Enqueue(buffered);
            }

            tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            _waiters.Add(new Pending(opcode, tcs, match));
        }

        byte[] packet = await tcs.Task.WaitAsync(timeout ?? TimeSpan.FromSeconds(10)).ConfigureAwait(false);
        return ReaderFor(packet);
    }

    private PacketReader ReaderFor(byte[] packet)
    {
        var r = new PacketReader(packet, _config.CodePage);
        r.ReadHeader();
        return r;
    }

    public override ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
    {
        // Re-frame the body with its opcode so a fresh reader can be handed to the waiter.
        byte[] body = packet.ReadBytes(packet.Remaining);
        byte[] full = new byte[2 + body.Length];
        full[0] = (byte)opcode;
        full[1] = (byte)(opcode >> 8);
        body.CopyTo(full, 2);

        lock (_gate)
        {
            for (int i = 0; i < _waiters.Count; i++)
            {
                if (_waiters[i].Opcode == opcode && (_waiters[i].Match is null || _waiters[i].Match!(ReaderFor(full))))
                {
                    Pending waiter = _waiters[i];
                    _waiters.RemoveAt(i);
                    waiter.Tcs.TrySetResult(full);
                    return ValueTask.CompletedTask;
                }
            }

            _unclaimed.Enqueue((opcode, full));
            while (_unclaimed.Count > 256)
            {
                _unclaimed.TryDequeue(out _); // bound the memory of chatty packets
            }
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);
}
