namespace MeatSpeak.Server.Voice;

using System.Collections.Concurrent;

public sealed class SsrcManager
{
    private uint _nextSsrc;
    private readonly ConcurrentDictionary<uint, string> _ssrcToSession = new();

    public uint Allocate(string sessionId)
    {
        const int maxRetries = 100;
        for (int i = 0; i < maxRetries; i++)
        {
            var ssrc = Interlocked.Increment(ref _nextSsrc);
            if (ssrc == 0)
                ssrc = Interlocked.Increment(ref _nextSsrc);
            if (_ssrcToSession.TryAdd(ssrc, sessionId))
                return ssrc;
        }
        throw new InvalidOperationException("Failed to allocate unique SSRC");
    }

    public void Release(uint ssrc) => _ssrcToSession.TryRemove(ssrc, out _);

    public string? GetSessionId(uint ssrc) =>
        _ssrcToSession.TryGetValue(ssrc, out var id) ? id : null;
}
