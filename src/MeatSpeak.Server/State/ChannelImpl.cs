namespace MeatSpeak.Server.State;

using System.Collections.Concurrent;
using MeatSpeak.Server.Core.Channels;

public sealed class ChannelImpl : IChannel
{
    private readonly ConcurrentDictionary<string, ChannelMembership> _members = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BanEntry> _bans = new();
    private readonly HashSet<string> _invites = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public string Name { get; }
    public string? Topic { get; set; }
    public string? TopicSetBy { get; set; }
    public DateTimeOffset? TopicSetAt { get; set; }
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, ChannelMembership> Members => _members;
    public HashSet<char> Modes { get; } = new();
    public string? Key { get; set; }
    public int? UserLimit { get; set; }

    public IReadOnlyList<BanEntry> Bans
    {
        get { lock (_lock) return _bans.ToList(); }
    }

    public IReadOnlyList<string> InviteList
    {
        get { lock (_lock) return _invites.ToList(); }
    }

    public ChannelImpl(string name)
    {
        Name = name;
        Modes.Add('n'); // no external messages by default
        Modes.Add('t'); // topic protected by default
    }

    public bool AddMember(string nickname, ChannelMembership membership)
        => _members.TryAdd(nickname, membership);

    public bool RemoveMember(string nickname)
        => _members.TryRemove(nickname, out _);

    public ChannelMembership? GetMember(string nickname)
        => _members.TryGetValue(nickname, out var m) ? m : null;

    public bool IsMember(string nickname)
        => _members.ContainsKey(nickname);

    public void AddBan(BanEntry ban)
    {
        lock (_lock) _bans.Add(ban);
    }

    public bool RemoveBan(string mask)
    {
        lock (_lock)
        {
            var idx = _bans.FindIndex(b => string.Equals(b.Mask, mask, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) { _bans.RemoveAt(idx); return true; }
            return false;
        }
    }

    public bool IsBanned(string mask)
    {
        lock (_lock) return _bans.Any(b => string.Equals(b.Mask, mask, StringComparison.OrdinalIgnoreCase));
    }

    public void AddInvite(string nickname)
    {
        lock (_lock) _invites.Add(nickname);
    }

    public bool IsInvited(string nickname)
    {
        lock (_lock) return _invites.Contains(nickname);
    }
}
