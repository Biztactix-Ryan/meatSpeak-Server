namespace MeatSpeak.Server.Transport.Tcp;

using System.Net;
using System.Net.Sockets;
using MeatSpeak.Server.Transport;
using MeatSpeak.Server.Transport.Pools;
using Microsoft.Extensions.Logging;

public sealed class TcpConnection : IConnection, IDisposable
{
    private readonly Socket _socket;
    private readonly SocketAsyncEventArgs _recvArgs;
    private readonly IConnectionHandler _handler;
    private readonly ILogger _logger;
    private readonly SocketEventArgsPool _sendPool;
    private readonly byte[] _recvBuffer;
    private int _recvOffset; // bytes of unconsumed data in buffer
    private bool _disposed;
    private readonly string _id;

    public string Id => _id;
    public EndPoint? RemoteEndPoint { get; }
    public bool IsConnected => !_disposed && _socket.Connected;

    public TcpConnection(
        Socket socket,
        IConnectionHandler handler,
        SocketEventArgsPool sendPool,
        ILogger logger)
    {
        _socket = socket;
        _socket.NoDelay = true;
        _handler = handler;
        _sendPool = sendPool;
        _logger = logger;
        _id = Guid.NewGuid().ToString("N")[..12];
        RemoteEndPoint = socket.RemoteEndPoint;

        // IRCv3 max line: 4096 (tags) + 512 (message) + 2 (CRLF) = 4610 bytes
        _recvBuffer = BufferPool.Rent(4610);
        _recvArgs = new SocketAsyncEventArgs(unsafeSuppressExecutionContextFlow: true);
        _recvArgs.SetBuffer(_recvBuffer, 0, _recvBuffer.Length);
        _recvArgs.Completed += OnReceiveCompleted;
        _recvArgs.UserToken = this;
    }

    public void StartReceiving()
    {
        _handler.OnConnected(this);
        BeginReceive();
    }

    private void BeginReceive()
    {
        if (_disposed) return;

        try
        {
            _recvArgs.SetBuffer(_recvOffset, _recvBuffer.Length - _recvOffset);
            if (!_socket.ReceiveAsync(_recvArgs))
            {
                // Completed synchronously — process inline (sync continuation loop)
                ProcessReceive(_recvArgs);
            }
        }
        catch (ObjectDisposedException) { }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "BeginReceive error on {Id}", _id);
            Disconnect();
        }
    }

    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        ProcessReceive(e);
    }

    private void ProcessReceive(SocketAsyncEventArgs e)
    {
        // Sync continuation loop — process all available data without re-entering event loop
        while (true)
        {
            if (e.SocketError != SocketError.Success || e.BytesTransferred == 0)
            {
                Disconnect();
                return;
            }

            _recvOffset += e.BytesTransferred;

            // Scan for complete lines
            int consumed = LineFramer.Scan(_recvBuffer, 0, _recvOffset, line =>
            {
                try
                {
                    _handler.OnData(this, line);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Handler error processing line on {Id}", _id);
                }
            });

            // Compact remaining data
            if (consumed > 0 && consumed < _recvOffset)
            {
                Buffer.BlockCopy(_recvBuffer, consumed, _recvBuffer, 0, _recvOffset - consumed);
                _recvOffset -= consumed;
            }
            else if (consumed > 0)
            {
                _recvOffset = 0;
            }

            // Buffer full with no complete line — overflow protection
            if (_recvOffset >= _recvBuffer.Length)
            {
                _logger.LogWarning("Receive buffer overflow on {Id}, disconnecting", _id);
                Disconnect();
                return;
            }

            if (_disposed) return;

            try
            {
                e.SetBuffer(_recvOffset, _recvBuffer.Length - _recvOffset);
                if (!_socket.ReceiveAsync(e))
                {
                    // Data ready synchronously — continue loop
                    continue;
                }
            }
            catch (ObjectDisposedException) { return; }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "ReceiveAsync error on {Id}", _id);
                Disconnect();
                return;
            }

            // ReceiveAsync returned true — will complete asynchronously via callback
            break;
        }
    }

    public void Send(ReadOnlySpan<byte> data)
    {
        if (_disposed) return;

        var sendArgs = _sendPool.Rent();
        data.CopyTo(sendArgs.Buffer.AsSpan());
        sendArgs.SetBuffer(0, data.Length);
        sendArgs.Completed += OnSendCompleted;

        try
        {
            if (!_socket.SendAsync(sendArgs))
                OnSendCompleted(null, sendArgs);
        }
        catch (ObjectDisposedException) { _sendPool.Return(sendArgs); }
        catch (Exception)
        {
            _sendPool.Return(sendArgs);
            Disconnect();
        }
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        Send(data.Span);
        return ValueTask.CompletedTask;
    }

    private void OnSendCompleted(object? sender, SocketAsyncEventArgs e)
    {
        e.Completed -= OnSendCompleted;
        _sendPool.Return(e);
    }

    public void Disconnect()
    {
        if (_disposed) return;
        Dispose();
        _handler.OnDisconnected(this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _socket.Shutdown(SocketShutdown.Both); } catch { }
        try { _socket.Close(); } catch { }

        _recvArgs.Completed -= OnReceiveCompleted;
        _recvArgs.Dispose();
        BufferPool.Return(_recvBuffer);
    }
}
