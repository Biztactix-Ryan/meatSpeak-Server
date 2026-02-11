namespace MeatSpeak.Server.Transport;

/// <summary>
/// Handles connection lifecycle events from any transport (TCP, WebSocket, etc.).
/// </summary>
public interface IConnectionHandler
{
    void OnConnected(IConnection connection);
    void OnData(IConnection connection, ReadOnlySpan<byte> line);
    void OnDisconnected(IConnection connection);
}
