namespace MeatSpeak.Benchmark;

public interface IIrcTransport : IAsyncDisposable
{
    Task ConnectAsync(string host, int port, CancellationToken ct);
    Task SendLineAsync(string line, CancellationToken ct);
    Task<string?> ReadLineAsync(CancellationToken ct);
    long BytesSent { get; }
    long BytesReceived { get; }
}
