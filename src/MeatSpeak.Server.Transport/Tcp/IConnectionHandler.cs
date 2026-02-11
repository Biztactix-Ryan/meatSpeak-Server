namespace MeatSpeak.Server.Transport.Tcp;

public interface IConnectionHandler
{
    void OnConnected(TcpConnection connection);
    void OnData(TcpConnection connection, ReadOnlySpan<byte> line);
    void OnDisconnected(TcpConnection connection);
}
