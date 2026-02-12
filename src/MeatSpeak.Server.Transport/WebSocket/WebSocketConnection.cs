namespace MeatSpeak.Server.Transport.WebSocket;

using System.Net;
using System.Net.WebSockets;
using MeatSpeak.Server.Transport.Tcp;
using Microsoft.Extensions.Logging;

/// <summary>
/// Wraps a System.Net.WebSockets.WebSocket as an IConnection.
/// IRC messages arrive as WebSocket text frames (one IRC line per frame, or CRLF-delimited within a frame).
/// Outgoing data is sent as text frames.
/// </summary>
public sealed class WebSocketConnection : IConnection, IDisposable
{
    private readonly System.Net.WebSockets.WebSocket _ws;
    private readonly IConnectionHandler _handler;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _id;
    private readonly EndPoint? _remoteEndPoint;
    private int _disposed;

    public string Id => _id;
    public EndPoint? RemoteEndPoint => _remoteEndPoint;
    public bool IsConnected => _disposed == 0 && _ws.State == WebSocketState.Open;

    public WebSocketConnection(
        System.Net.WebSockets.WebSocket ws,
        IConnectionHandler handler,
        EndPoint? remoteEndPoint,
        ILogger logger)
    {
        _ws = ws;
        _handler = handler;
        _remoteEndPoint = remoteEndPoint;
        _logger = logger;
        _id = "ws-" + Guid.NewGuid().ToString("N")[..12];
    }

    /// <summary>
    /// Starts the receive loop. Call this after constructing the connection.
    /// This method runs until the WebSocket closes and will call OnConnected/OnData/OnDisconnected.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _handler.OnConnected(this);

        var buffer = new byte[4610];
        var lineBuffer = new byte[4610];
        int lineOffset = 0;

        try
        {
            while (!ct.IsCancellationRequested && _ws.State == WebSocketState.Open)
            {
                WebSocketReceiveResult result;
                try
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                }
                catch (WebSocketException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    // Complete the close handshake so the client's CloseAsync returns
                    if (_ws.State == WebSocketState.CloseReceived)
                    {
                        try
                        {
                            await _ws.CloseOutputAsync(
                                WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                        }
                        catch { }
                    }
                    break;
                }

                // Copy received data into line buffer and scan for complete IRC lines
                int received = result.Count;
                if (lineOffset + received > lineBuffer.Length)
                {
                    // Overflow — disconnect
                    _logger.LogWarning("WebSocket receive buffer overflow on {Id}, disconnecting", _id);
                    break;
                }

                Buffer.BlockCopy(buffer, 0, lineBuffer, lineOffset, received);
                lineOffset += received;

                // If the frame is a complete message (EndOfMessage) and contains no newline,
                // treat the entire frame as a single IRC line (per IRCv3 WebSocket spec)
                bool hasNewline = Array.IndexOf(lineBuffer, (byte)'\n', 0, lineOffset) >= 0;

                if (hasNewline)
                {
                    // Standard CRLF framing within the data
                    int consumed = LineFramer.Scan(lineBuffer, 0, lineOffset, line =>
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

                    if (consumed > 0 && consumed < lineOffset)
                    {
                        Buffer.BlockCopy(lineBuffer, consumed, lineBuffer, 0, lineOffset - consumed);
                        lineOffset -= consumed;
                    }
                    else if (consumed > 0)
                    {
                        lineOffset = 0;
                    }
                }
                else if (result.EndOfMessage && lineOffset > 0)
                {
                    // Complete WebSocket message with no newline — treat as single IRC line
                    // Strip trailing \r if present
                    int len = lineOffset;
                    if (len > 0 && lineBuffer[len - 1] == (byte)'\r')
                        len--;

                    if (len > 0)
                    {
                        try
                        {
                            _handler.OnData(this, new ReadOnlySpan<byte>(lineBuffer, 0, len));
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Handler error processing line on {Id}", _id);
                        }
                    }
                    lineOffset = 0;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "WebSocket receive loop error on {Id}", _id);
        }
        finally
        {
            Dispose();
            _handler.OnDisconnected(this);
        }
    }

    public void Send(ReadOnlySpan<byte> data)
    {
        if (_disposed != 0) return;

        // Send as a text frame (IRC over WebSocket uses text frames)
        // We need to copy since WebSocket SendAsync requires a buffer that lives past this call
        var copy = data.ToArray();
        _ = SendInternalAsync(copy);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        if (_disposed != 0) return ValueTask.CompletedTask;

        var copy = data.ToArray();
        _ = SendInternalAsync(copy);
        return ValueTask.CompletedTask;
    }

    private async Task SendInternalAsync(byte[] data)
    {
        await _writeLock.WaitAsync();
        try
        {
            if (_ws.State == WebSocketState.Open)
            {
                await _ws.SendAsync(
                    new ArraySegment<byte>(data),
                    WebSocketMessageType.Text,
                    endOfMessage: true,
                    CancellationToken.None);
            }
        }
        catch (WebSocketException)
        {
            // Connection lost during send
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public void Disconnect()
    {
        if (_disposed != 0) return;

        // Abort the WebSocket immediately. This unblocks any pending ReceiveAsync
        // in the RunAsync loop, which will then call OnDisconnected and Dispose.
        try { _ws.Abort(); } catch { }
    }

    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

        try { _ws.Dispose(); } catch { }
        _writeLock.Dispose();
    }
}
