namespace MeatSpeak.Server.Transport;

using System.Net;

/// <summary>
/// Transport-agnostic connection abstraction. Implemented by TcpConnection, WebSocketConnection, etc.
/// </summary>
public interface IConnection
{
    string Id { get; }
    EndPoint? RemoteEndPoint { get; }
    bool IsConnected { get; }
    void Send(ReadOnlySpan<byte> data);
    ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken ct = default);
    void Disconnect();
}
