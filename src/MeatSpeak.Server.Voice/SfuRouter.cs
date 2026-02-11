namespace MeatSpeak.Server.Voice;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Transport.Udp;
using Microsoft.Extensions.Logging;

public sealed class SfuRouter
{
    private readonly SsrcManager _ssrcManager;
    private readonly TransportEncryption _encryption;
    private readonly SilenceDetector _silenceDetector;
    private readonly UdpSender? _sender;
    private readonly ILogger<SfuRouter> _logger;
    private readonly Dictionary<string, VoiceChannel> _channels = new(StringComparer.OrdinalIgnoreCase);

    public SfuRouter(
        SsrcManager ssrcManager,
        TransportEncryption encryption,
        SilenceDetector silenceDetector,
        UdpSender? sender,
        ILogger<SfuRouter> logger)
    {
        _ssrcManager = ssrcManager;
        _encryption = encryption;
        _silenceDetector = silenceDetector;
        _sender = sender;
        _logger = logger;
    }

    public VoiceChannel GetOrCreateChannel(string name)
    {
        if (!_channels.TryGetValue(name, out var channel))
        {
            channel = new VoiceChannel(name);
            _channels[name] = channel;
        }
        return channel;
    }

    public void RemoveChannel(string name) => _channels.Remove(name);

    public void Route(VoicePacket packet, VoiceChannel channel, VoiceSession sender)
    {
        if (_sender == null) return;

        if (!packet.HasE2E && _silenceDetector.IsSilence(packet.Payload))
            return;

        foreach (var (id, listener) in channel.Members)
        {
            if (id == sender.SessionId) continue;
            if (listener.IsDeafened || listener.IsSelfDeafened) continue;
            if (listener.Endpoint == null || listener.TransportKey == null) continue;

            var buffer = new byte[VoicePacket.HeaderSize + packet.Payload.Length];
            VoicePacket.Write(buffer, packet.Type, packet.Flags, packet.Ssrc, packet.Sequence, packet.Timestamp, packet.Payload);
            _sender.SendTo(buffer, listener.Endpoint);
        }
    }
}
