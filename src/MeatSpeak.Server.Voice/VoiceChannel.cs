namespace MeatSpeak.Server.Voice;

using System.Collections.Concurrent;

public sealed class VoiceChannel
{
    public string Name { get; }
    public ConcurrentDictionary<string, VoiceSession> Members { get; } = new();
    public string? PrioritySpeaker { get; set; }
    public byte[]? GroupKey { get; set; }

    public VoiceChannel(string name) => Name = name;

    public bool AddMember(VoiceSession session) => Members.TryAdd(session.SessionId, session);
    public bool RemoveMember(string sessionId) => Members.TryRemove(sessionId, out _);
}
