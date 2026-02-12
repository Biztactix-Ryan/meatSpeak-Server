namespace MeatSpeak.Server.Transport.Tls;

using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using MeatSpeak.Server.Transport.Pools;
using MeatSpeak.Server.Transport.Tcp;
using Microsoft.Extensions.Logging;

public sealed class TlsTcpConnection : IConnection, IDisposable
{
    private readonly Socket _socket;
    private readonly SslStream _sslStream;
    private readonly IConnectionHandler _handler;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _id;
    private volatile bool _disposed;

    public string Id => _id;
    public EndPoint? RemoteEndPoint { get; }
    public bool IsConnected => !_disposed && _socket.Connected;

    public TlsTcpConnection(
        Socket socket,
        SslStream sslStream,
        IConnectionHandler handler,
        ILogger logger)
    {
        _socket = socket;
        _sslStream = sslStream;
        _handler = handler;
        _logger = logger;
        _id = "tls-" + Guid.NewGuid().ToString("N")[..12];
        RemoteEndPoint = socket.RemoteEndPoint;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _handler.OnConnected(this);

        var buffer = BufferPool.Rent(4610);
        int offset = 0;

        try
        {
            while (!ct.IsCancellationRequested && !_disposed)
            {
                int bytesRead;
                try
                {
                    bytesRead = await _sslStream.ReadAsync(
                        buffer.AsMemory(offset, buffer.Length - offset), ct);
                }
                catch (OperationCanceledException) { break; }
                catch (IOException) { break; }
                catch (ObjectDisposedException) { break; }

                if (bytesRead == 0)
                    break;

                offset += bytesRead;

                int consumed = LineFramer.Scan(buffer, 0, offset, line =>
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

                if (consumed > 0 && consumed < offset)
                {
                    Buffer.BlockCopy(buffer, consumed, buffer, 0, offset - consumed);
                    offset -= consumed;
                }
                else if (consumed > 0)
                {
                    offset = 0;
                }

                if (offset >= buffer.Length)
                {
                    _logger.LogWarning("Receive buffer overflow on {Id}, disconnecting", _id);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "TLS receive loop error on {Id}", _id);
        }
        finally
        {
            BufferPool.Return(buffer);
            Dispose();
            _handler.OnDisconnected(this);
        }
    }

    public void Send(ReadOnlySpan<byte> data)
    {
        if (_disposed) return;
        var copy = data.ToArray();
        _ = SendInternalAsync(copy);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed) return ValueTask.CompletedTask;
        var copy = data.ToArray();
        _ = SendInternalAsync(copy);
        return ValueTask.CompletedTask;
    }

    private async Task SendInternalAsync(byte[] data)
    {
        await _writeLock.WaitAsync();
        try
        {
            if (!_disposed)
            {
                await _sslStream.WriteAsync(data);
                await _sslStream.FlushAsync();
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            _writeLock.Release();
        }
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

        try { _sslStream.Close(); } catch { }
        try { _socket.Shutdown(SocketShutdown.Both); } catch { }
        try { _socket.Close(); } catch { }
        _sslStream.Dispose();
        _writeLock.Dispose();
    }
}
