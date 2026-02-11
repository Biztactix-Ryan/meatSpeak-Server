namespace MeatSpeak.Server.Transport.Udp;

using System.Net;
using System.Net.Sockets;
using MeatSpeak.Server.Transport.Pools;
using Microsoft.Extensions.Logging;

public sealed class UdpListener : IDisposable
{
    private readonly IUdpPacketHandler _handler;
    private readonly ILogger<UdpListener> _logger;
    private Socket? _socket;
    private readonly SocketEventArgsPool _recvPool;
    private readonly int _concurrentRecvs;
    private bool _disposed;

    public UdpListener(IUdpPacketHandler handler, ILogger<UdpListener> logger, int concurrentRecvs = 4)
    {
        _handler = handler;
        _logger = logger;
        _concurrentRecvs = concurrentRecvs;
        _recvPool = new SocketEventArgsPool(bufferSize: 1500, preAllocate: concurrentRecvs);
    }

    public void Start(IPEndPoint endPoint)
    {
        _socket = new Socket(endPoint.AddressFamily, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(endPoint);
        _logger.LogInformation("UDP listener started on {EndPoint}", endPoint);

        for (int i = 0; i < _concurrentRecvs; i++)
            BeginReceive();
    }

    private void BeginReceive()
    {
        if (_disposed) return;

        var args = _recvPool.Rent();
        args.RemoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
        args.Completed += OnReceiveCompleted;

        try
        {
            if (!_socket!.ReceiveFromAsync(args))
                ProcessReceive(args);
        }
        catch (ObjectDisposedException) { _recvPool.Return(args); }
    }

    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        ProcessReceive(e);
    }

    private void ProcessReceive(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
        {
            var data = new ReadOnlySpan<byte>(e.Buffer, e.Offset, e.BytesTransferred);
            try
            {
                _handler.OnPacketReceived(data, e.RemoteEndPoint!);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling UDP packet from {Remote}", e.RemoteEndPoint);
            }
        }

        e.Completed -= OnReceiveCompleted;
        _recvPool.Return(e);
        BeginReceive();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _socket?.Close(); } catch { }
        _socket?.Dispose();
    }
}
