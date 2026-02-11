namespace MeatSpeak.Server.Transport.Udp;

using System.Net;
using System.Net.Sockets;
using MeatSpeak.Server.Transport.Pools;
using Microsoft.Extensions.Logging;

public sealed class UdpSender : IDisposable
{
    private readonly Socket _socket;
    private readonly SocketEventArgsPool _sendPool;
    private readonly ILogger<UdpSender> _logger;
    private bool _disposed;

    public UdpSender(Socket socket, ILogger<UdpSender> logger)
    {
        _socket = socket;
        _logger = logger;
        _sendPool = new SocketEventArgsPool(bufferSize: 1500, preAllocate: 8);
    }

    public void SendTo(ReadOnlySpan<byte> data, EndPoint remoteEndPoint)
    {
        if (_disposed) return;

        var args = _sendPool.Rent();
        data.CopyTo(args.Buffer.AsSpan());
        args.SetBuffer(0, data.Length);
        args.RemoteEndPoint = remoteEndPoint;
        args.Completed += OnSendCompleted;

        try
        {
            if (!_socket.SendToAsync(args))
                OnSendCompleted(null, args);
        }
        catch (ObjectDisposedException) { _sendPool.Return(args); }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "UDP send error to {Remote}", remoteEndPoint);
            _sendPool.Return(args);
        }
    }

    private void OnSendCompleted(object? sender, SocketAsyncEventArgs e)
    {
        e.Completed -= OnSendCompleted;
        _sendPool.Return(e);
    }

    public void Dispose()
    {
        _disposed = true;
    }
}
