using System.Net.WebSockets;
using System.Text;

namespace MeatSpeak.Benchmark;

public sealed class WsTransport : IIrcTransport
{
    private readonly string _path;
    private ClientWebSocket? _ws;
    private long _bytesSent;
    private long _bytesReceived;
    private readonly byte[] _recvBuffer = new byte[4096];
    private string _leftover = "";

    public long BytesSent => _bytesSent;
    public long BytesReceived => _bytesReceived;

    public WsTransport(string path = "/irc")
    {
        _path = path;
    }

    public async Task ConnectAsync(string host, int port, CancellationToken ct)
    {
        _ws = new ClientWebSocket();
        var uri = new Uri($"ws://{host}:{port}{_path}");
        await _ws.ConnectAsync(uri, ct);
    }

    public async Task SendLineAsync(string line, CancellationToken ct)
    {
        if (_ws == null) throw new InvalidOperationException("Not connected");
        var data = Encoding.UTF8.GetBytes(line + "\r\n");
        await _ws.SendAsync(data.AsMemory(), WebSocketMessageType.Text, true, ct);
        Interlocked.Add(ref _bytesSent, data.Length);
    }

    public async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        if (_ws == null) throw new InvalidOperationException("Not connected");

        while (true)
        {
            // Check leftover buffer for a complete line
            var nlIndex = _leftover.IndexOf('\n');
            if (nlIndex >= 0)
            {
                var line = _leftover[..nlIndex].TrimEnd('\r');
                _leftover = _leftover[(nlIndex + 1)..];
                return line;
            }

            var result = await _ws.ReceiveAsync(_recvBuffer.AsMemory(), ct);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;

            var text = Encoding.UTF8.GetString(_recvBuffer, 0, result.Count);
            Interlocked.Add(ref _bytesReceived, result.Count);
            _leftover += text;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_ws != null)
        {
            if (_ws.State == WebSocketState.Open)
            {
                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", cts.Token);
                }
                catch { /* best effort */ }
            }
            _ws.Dispose();
        }
    }
}
