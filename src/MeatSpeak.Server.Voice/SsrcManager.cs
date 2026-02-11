namespace MeatSpeak.Server.Voice;

using System.Collections.Concurrent;

public sealed class SsrcManager
{
    private uint _nextSsrc;
    private readonly ConcurrentDictionary<uint, string> _ssrcToSession = new();

    public uint Allocate(string sessionId)
    {
        var ssrc = Interlocked.Increment(ref _nextSsrc);
        _ssrcToSession[ssrc] = sessionId;
        return ssrc;
    }

    public void Release(uint ssrc) => _ssrcToSession.TryRemove(ssrc, out _);

    public string? GetSessionId(uint ssrc) =>
        _ssrcToSession.TryGetValue(ssrc, out var id) ? id : null;
}
