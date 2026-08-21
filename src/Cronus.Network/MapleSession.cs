using System.Buffers;
using System.Buffers.Binary;
using System.IO.Pipelines;
using System.Security.Cryptography;
using Cronus.Common;
using Cronus.Network.Crypto;
using Cronus.Network.Packets;

namespace Cronus.Network;

public enum SessionRole
{
    /// <summary>Generates the IV pair and sends the Hello packet.</summary>
    Server,

    /// <summary>Receives the Hello packet and derives its ciphers from it.</summary>
    Client,
}

/// <summary>
/// One MapleStory connection: owns the send/receive <see cref="AesOfbCipher"/> pair, performs
/// the Hello handshake, and runs the framed read loop (ports the roles of
/// <c>tacos.network.PacketHandler</c> + <c>PacketEncoder</c>/<c>PacketDecoder</c>, rewritten
/// on <see cref="System.IO.Pipelines"/>).
///
/// Wire flow after connect:
/// <list type="number">
///   <item>Server sends a plaintext Hello: <c>[size:2][version:2][subVer][recvIv:4][sendIv:4][region:1]</c>.</item>
///   <item>Both sides then exchange framed packets: <c>[header:4][encrypted body]</c>.</item>
/// </list>
/// </summary>
public sealed class MapleSession : IAsyncDisposable
{
    private const int HeaderLength = 4;

    private readonly PipeReader _input;
    private readonly PipeWriter _output;
    private readonly ServerConfig _config;
    private readonly SessionRole _role;
    private readonly IPacketHandler _handler;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly Func<byte>? _randomByte;
    private readonly byte[]? _keepAlive;
    private readonly TimeSpan _keepAliveInterval;

    private AesOfbCipher? _sendCipher;
    private AesOfbCipher? _recvCipher;
    private bool _disposed;

    public MapleSession(
        PipeReader input,
        PipeWriter output,
        ServerConfig config,
        SessionRole role,
        IPacketHandler handler,
        Func<byte>? randomByte = null,
        byte[]? keepAlive = null,
        TimeSpan? keepAliveInterval = null)
    {
        _input = input;
        _output = output;
        _config = config;
        _role = role;
        _handler = handler;
        _randomByte = randomByte;
        _keepAlive = keepAlive;
        _keepAliveInterval = keepAliveInterval ?? TimeSpan.FromSeconds(15);
    }

    /// <summary>Arbitrary per-session state bag for higher layers (e.g. account, stage).</summary>
    public object? UserData { get; set; }

    public ServerConfig Config => _config;

    public SessionRole Role => _role;

    /// <summary>
    /// Runs the session to completion: handshake, connected callback, then the receive loop
    /// until the peer closes or an error occurs.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Exception? error = null;
        try
        {
            if (_role == SessionRole.Server)
            {
                await SendHelloAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await ReceiveHelloAsync(cancellationToken).ConfigureAwait(false);
            }

            await _handler.OnConnectedAsync(this).ConfigureAwait(false);

            // A server session pings the client so it does not consider the link idle.
            using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            Task keepAlive = _role == SessionRole.Server && _keepAlive is not null
                ? KeepAliveLoopAsync(loopCts.Token)
                : Task.CompletedTask;

            try
            {
                await ReceiveLoopAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                loopCts.Cancel();
                await keepAlive.ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            error = ex;
        }
        finally
        {
            await _handler.OnDisconnectedAsync(this, error).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Frames, encrypts, and writes one packet body (which already includes its opcode header).
    /// Thread-safe: concurrent sends are serialized.
    /// </summary>
    /// <summary>
    /// Optional diagnostic hook invoked with the plaintext body of every outbound packet
    /// (before framing/encryption). Set by the host for wire logging; null in production.
    /// </summary>
    public static Action<SessionRole, ReadOnlyMemory<byte>>? DebugOnSend { get; set; }

    public async ValueTask SendAsync(ReadOnlyMemory<byte> body, CancellationToken cancellationToken = default)
    {
        if (_sendCipher is null)
        {
            throw new InvalidOperationException("Cannot send before the handshake completes.");
        }

        DebugOnSend?.Invoke(_role, body);

        await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[] payload = body.ToArray();
            Memory<byte> frame = _output.GetMemory(HeaderLength + payload.Length);

            _sendCipher.WriteHeader(frame.Span, payload.Length);

            if (_config.CustomEncryption)
            {
                ShandaCipher.Encrypt(payload);
            }

            _sendCipher.Crypt(payload);
            _sendCipher.AdvanceIv();

            payload.CopyTo(frame.Span[HeaderLength..]);
            _output.Advance(HeaderLength + payload.Length);

            FlushResult result = await _output.FlushAsync(cancellationToken).ConfigureAwait(false);
            if (result.IsCompleted)
            {
                throw new IOException("The connection was closed by the peer.");
            }
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task KeepAliveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_keepAliveInterval, cancellationToken).ConfigureAwait(false);
                await SendAsync(_keepAlive!, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Session ending.
        }
        catch (Exception)
        {
            // The connection is going away; the receive loop reports the disconnect.
        }
    }

    private async ValueTask SendHelloAsync(CancellationToken cancellationToken)
    {
        // serverRecv drives the receive (client→server) cipher; serverSend the send cipher.
        byte[] serverRecv = { 70, 114, 122, NextRandomByte() };
        byte[] serverSend = { 82, 48, 120, NextRandomByte() };

        _sendCipher = new AesOfbCipher(serverSend, _config.Version, outbound: true);
        _recvCipher = new AesOfbCipher(serverRecv, _config.Version, outbound: false);

        byte[] hello = Handshake.BuildHello(_config, serverRecv, serverSend);
        await _output.WriteAsync(hello, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask ReceiveHelloAsync(CancellationToken cancellationToken)
    {
        // Hello has its own 2-byte little-endian size prefix (not the 4-byte frame header).
        byte[] sizeBytes = await ReadExactAsync(2, cancellationToken).ConfigureAwait(false);
        int bodySize = BinaryPrimitives.ReadUInt16LittleEndian(sizeBytes);
        byte[] body = await ReadExactAsync(bodySize, cancellationToken).ConfigureAwait(false);

        var reader = new PacketReader(body, _config.CodePage);
        _ = reader.ReadShort();        // version
        _ = reader.ReadString();       // sub-version string
        byte[] recvIv = reader.ReadBytes(4);
        byte[] sendIv = reader.ReadBytes(4);
        _ = reader.ReadByte();         // region

        // Mirror the server: our send cipher pairs with the server's recv cipher, and vice
        // versa, so IV sequences and version markers line up.
        _sendCipher = new AesOfbCipher(recvIv, _config.Version, outbound: false);
        _recvCipher = new AesOfbCipher(sendIv, _config.Version, outbound: true);
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            byte[] header = await ReadExactAsync(HeaderLength, cancellationToken).ConfigureAwait(false);
            if (header.Length == 0)
            {
                return; // peer closed cleanly
            }

            if (!_recvCipher!.CheckHeader(header))
            {
                throw new InvalidDataException("Invalid packet header (version/IV mismatch).");
            }

            int length = AesOfbCipher.ReadLength(header);
            byte[] body = await ReadExactAsync(length, cancellationToken).ConfigureAwait(false);
            if (body.Length < length)
            {
                return; // truncated; peer closed
            }

            _recvCipher.Crypt(body);
            if (_config.CustomEncryption)
            {
                ShandaCipher.Decrypt(body);
            }

            _recvCipher.AdvanceIv();

            var packet = new PacketReader(body, _config.CodePage);
            int opcode = packet.ReadHeader(_config.PacketHeaderSize);
            await _handler.OnPacketAsync(this, opcode, packet).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reads exactly <paramref name="count"/> bytes. Returns a shorter/empty array only if the
    /// peer closes before that many bytes arrive.
    /// </summary>
    private async ValueTask<byte[]> ReadExactAsync(int count, CancellationToken cancellationToken)
    {
        if (count == 0)
        {
            return Array.Empty<byte>();
        }

        while (true)
        {
            ReadResult result = await _input.ReadAsync(cancellationToken).ConfigureAwait(false);
            ReadOnlySequence<byte> buffer = result.Buffer;

            if (buffer.Length >= count)
            {
                ReadOnlySequence<byte> slice = buffer.Slice(0, count);
                byte[] data = slice.ToArray();
                _input.AdvanceTo(slice.End);
                return data;
            }

            if (result.IsCompleted)
            {
                // Not enough bytes and no more coming.
                _input.AdvanceTo(buffer.Start, buffer.End);
                return buffer.Length == 0 ? Array.Empty<byte>() : buffer.ToArray();
            }

            // Need more data: mark everything examined so the next read waits for new bytes.
            _input.AdvanceTo(buffer.Start, buffer.End);
        }
    }

    private byte NextRandomByte()
        => _randomByte?.Invoke() ?? (byte)RandomNumberGenerator.GetInt32(256);

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _sendCipher?.Dispose();
        _recvCipher?.Dispose();
        _sendLock.Dispose();
        await _input.CompleteAsync().ConfigureAwait(false);
        await _output.CompleteAsync().ConfigureAwait(false);
    }
}
