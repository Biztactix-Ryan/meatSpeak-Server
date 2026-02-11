namespace MeatSpeak.Server.Transport.Udp;

using System.Net;

public interface IUdpPacketHandler
{
    void OnPacketReceived(ReadOnlySpan<byte> data, EndPoint remoteEndPoint);
}
