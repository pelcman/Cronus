using System.IO.Pipelines;
using System.Net;
using System.Net.Sockets;
using Cronus.Common;

namespace Cronus.Network;

/// <summary>
/// Accepts TCP connections and runs a server-role <see cref="MapleSession"/> for each
/// (ports the acceptor role of <c>tacos.network.PacketHandler</c>). A fresh
/// <see cref="IPacketHandler"/> is created per connection via the supplied factory.
/// </summary>
public sealed class MapleListener : IAsyncDisposable
{
    private readonly IPEndPoint _endpoint;
    private readonly ServerConfig _config;
    private readonly Func<IPacketHandler> _handlerFactory;
    private readonly List<Task> _sessions = new();
    private readonly object _sessionsLock = new();

    private readonly byte[]? _keepAlive;

    private Socket? _listenSocket;

    public MapleListener(
        IPEndPoint endpoint,
        ServerConfig config,
        Func<IPacketHandler> handlerFactory,
        byte[]? keepAlive = null)
    {
        _endpoint = endpoint;
        _config = config;
        _handlerFactory = handlerFactory;
        _keepAlive = keepAlive;
    }

    /// <summary>The bound endpoint (useful when binding to port 0 to get an ephemeral port).</summary>
    public IPEndPoint? LocalEndpoint => _listenSocket?.LocalEndPoint as IPEndPoint;

    /// <summary>Binds and starts accepting. Returns when accepting stops (cancellation/close).</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        _listenSocket = new Socket(_endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.Bind(_endpoint);
        _listenSocket.Listen(backlog: 128);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                Socket client = await _listenSocket.AcceptAsync(cancellationToken).ConfigureAwait(false);
                client.NoDelay = true;
                TrackSession(RunSessionAsync(client, cancellationToken));
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (ObjectDisposedException)
        {
            // Listener closed.
        }
    }

    private async Task RunSessionAsync(Socket socket, CancellationToken cancellationToken)
    {
        var stream = new NetworkStream(socket, ownsSocket: true);
        var input = PipeReader.Create(stream);
        var output = PipeWriter.Create(stream);
        await using var session = new MapleSession(
            input, output, _config, SessionRole.Server, _handlerFactory(), keepAlive: _keepAlive);

        try
        {
            await session.RunAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await stream.DisposeAsync().ConfigureAwait(false);
        }
    }

    private void TrackSession(Task sessionTask)
    {
        lock (_sessionsLock)
        {
            _sessions.RemoveAll(t => t.IsCompleted);
            _sessions.Add(sessionTask);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _listenSocket?.Dispose();

        Task[] pending;
        lock (_sessionsLock)
        {
            pending = _sessions.ToArray();
        }

        try
        {
            await Task.WhenAll(pending).ConfigureAwait(false);
        }
        catch
        {
            // Individual session failures are surfaced through their handlers.
        }
    }
}
