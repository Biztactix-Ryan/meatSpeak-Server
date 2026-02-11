using System.Net.Sockets;
using System.Text;

namespace MeatSpeak.Benchmark;

public sealed class TcpTransport : IIrcTransport
{
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private long _bytesSent;
    private long _bytesReceived;

    public long BytesSent => _bytesSent;
    public long BytesReceived => _bytesReceived;

    public async Task ConnectAsync(string host, int port, CancellationToken ct)
    {
        _client = new TcpClient();
        await _client.ConnectAsync(host, port, ct);
        var stream = _client.GetStream();
        var utf8NoBom = new UTF8Encoding(false);
        _reader = new StreamReader(stream, utf8NoBom);
        _writer = new StreamWriter(stream, utf8NoBom) { NewLine = "\r\n", AutoFlush = true };
    }

    public async Task SendLineAsync(string line, CancellationToken ct)
    {
        if (_writer == null) throw new InvalidOperationException("Not connected");
        await _writer.WriteLineAsync(line.AsMemory(), ct);
        Interlocked.Add(ref _bytesSent, Encoding.UTF8.GetByteCount(line) + 2); // +2 for CRLF
    }

    public async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        if (_reader == null) throw new InvalidOperationException("Not connected");
        var line = await _reader.ReadLineAsync(ct);
        if (line != null)
            Interlocked.Add(ref _bytesReceived, Encoding.UTF8.GetByteCount(line) + 2);
        return line;
    }

    public async ValueTask DisposeAsync()
    {
        if (_writer != null) await _writer.DisposeAsync();
        _reader?.Dispose();
        _client?.Dispose();
    }
}
