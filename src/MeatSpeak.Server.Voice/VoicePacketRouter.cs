namespace MeatSpeak.Server.Voice;

using System.Collections.Concurrent;
using System.Net;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Transport.Udp;
using Microsoft.Extensions.Logging;

public sealed class VoicePacketRouter : IUdpPacketHandler
{
    private readonly SfuRouter _sfuRouter;
    private readonly SsrcManager _ssrcManager;
    private readonly TransportEncryption _encryption;
    private readonly ConcurrentDictionary<string, VoiceSession> _sessionsByEndpoint = new();
    private readonly ILogger<VoicePacketRouter> _logger;

    public VoicePacketRouter(
        SfuRouter sfuRouter,
        SsrcManager ssrcManager,
        TransportEncryption encryption,
        ILogger<VoicePacketRouter> logger)
    {
        _sfuRouter = sfuRouter;
        _ssrcManager = ssrcManager;
        _encryption = encryption;
        _logger = logger;
    }

    public void RegisterSession(VoiceSession session, EndPoint endpoint)
    {
        session.Endpoint = endpoint;
        _sessionsByEndpoint[endpoint.ToString()!] = session;
    }

    public void UnregisterSession(VoiceSession session)
    {
        if (session.Endpoint != null)
            _sessionsByEndpoint.TryRemove(session.Endpoint.ToString()!, out _);
    }

    public void OnPacketReceived(ReadOnlySpan<byte> data, EndPoint remoteEndPoint)
    {
        if (!_sessionsByEndpoint.TryGetValue(remoteEndPoint.ToString()!, out var session))
        {
            _logger.LogDebug("Packet from unknown endpoint {Endpoint}", remoteEndPoint);
            return;
        }

        if (!VoicePacket.TryParse(data, out var packet))
        {
            _logger.LogDebug("Invalid voice packet from {Session}", session.SessionId);
            return;
        }

        if (packet.Ssrc != session.Ssrc)
        {
            _logger.LogDebug("SSRC mismatch from {Session}", session.SessionId);
            return;
        }

        if (session.CurrentChannel == null) return;
        var channel = _sfuRouter.GetOrCreateChannel(session.CurrentChannel);
        _sfuRouter.Route(packet, channel, session);
    }
}
