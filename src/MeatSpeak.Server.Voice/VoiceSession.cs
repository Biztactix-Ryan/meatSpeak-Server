namespace MeatSpeak.Server.Voice;

using System.Net;

public sealed class VoiceSession
{
    public string SessionId { get; }
    public uint Ssrc { get; }
    public EndPoint? Endpoint { get; set; }
    public byte[]? TransportKey { get; set; }
    public string? CurrentChannel { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSelfMuted { get; set; }
    public bool IsSelfDeafened { get; set; }
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    public VoiceSession(string sessionId, uint ssrc)
    {
        SessionId = sessionId;
        Ssrc = ssrc;
    }
}
