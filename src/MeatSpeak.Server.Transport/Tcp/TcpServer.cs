namespace MeatSpeak.Server.Transport.Tcp;

using System.Net;
using System.Net.Sockets;
using MeatSpeak.Server.Transport;
using MeatSpeak.Server.Transport.Pools;
using Microsoft.Extensions.Logging;

public sealed class TcpServer : IDisposable
{
    private readonly IConnectionHandler _handler;
    private readonly ILogger<TcpServer> _logger;
    private readonly SocketEventArgsPool _sendPool;
    private Socket? _listenSocket;
    private SocketAsyncEventArgs? _acceptArgs;
    private bool _disposed;

    public TcpServer(IConnectionHandler handler, ILogger<TcpServer> logger, SocketEventArgsPool? sendPool = null)
    {
        _handler = handler;
        _logger = logger;
        _sendPool = sendPool ?? new SocketEventArgsPool(4608, preAllocate: 16);
    }

    public void Start(IPEndPoint endPoint, int backlog = 2048)
    {
        _listenSocket = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _listenSocket.Bind(endPoint);
        _listenSocket.Listen(backlog);

        _logger.LogInformation("TCP server listening on {EndPoint}", endPoint);

        _acceptArgs = new SocketAsyncEventArgs(unsafeSuppressExecutionContextFlow: true);
        _acceptArgs.Completed += OnAcceptCompleted;
        BeginAccept();
    }

    private void BeginAccept()
    {
        if (_disposed || _acceptArgs == null) return;

        _acceptArgs.AcceptSocket = null;
        try
        {
            if (!_listenSocket!.AcceptAsync(_acceptArgs))
                ProcessAccept(_acceptArgs);
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Accept error");
        }
    }

    private void OnAcceptCompleted(object? sender, SocketAsyncEventArgs e)
    {
        ProcessAccept(e);
    }

    private void ProcessAccept(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success && e.AcceptSocket != null)
        {
            var clientSocket = e.AcceptSocket;
            _logger.LogDebug("Accepted connection from {Remote}", clientSocket.RemoteEndPoint);

            var connection = new TcpConnection(
                clientSocket,
                _handler,
                _sendPool,
                _logger);
            connection.StartReceiving();
        }
        else if (e.SocketError != SocketError.OperationAborted)
        {
            _logger.LogWarning("Accept failed: {Error}", e.SocketError);
        }

        // Accept next connection
        BeginAccept();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _acceptArgs?.Dispose();
        try { _listenSocket?.Close(); } catch { }
        _listenSocket?.Dispose();
    }
}
