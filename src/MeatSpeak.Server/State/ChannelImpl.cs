namespace MeatSpeak.Server.State;

using System.Collections.Concurrent;
using MeatSpeak.Server.Core.Channels;

public sealed class ChannelImpl : IChannel
{
    private readonly ConcurrentDictionary<string, ChannelMembership> _members = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<BanEntry> _bans = new();
    private readonly List<BanEntry> _excepts = new();
    private readonly HashSet<string> _invites = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    private string? _topic;
    private string? _topicSetBy;
    private DateTimeOffset? _topicSetAt;
    private string? _key;
    private int? _userLimit;

    public string Name { get; }

    public string? Topic
    {
        get { lock (_lock) return _topic; }
        set { lock (_lock) _topic = value; }
    }

    public string? TopicSetBy
    {
        get { lock (_lock) return _topicSetBy; }
        set { lock (_lock) _topicSetBy = value; }
    }

    public DateTimeOffset? TopicSetAt
    {
        get { lock (_lock) return _topicSetAt; }
        set { lock (_lock) _topicSetAt = value; }
    }

    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public IReadOnlyDictionary<string, ChannelMembership> Members => _members;
    public HashSet<char> Modes { get; } = new();

    public string? Key
    {
        get { lock (_lock) return _key; }
        set { lock (_lock) _key = value; }
    }

    public int? UserLimit
    {
        get { lock (_lock) return _userLimit; }
        set { lock (_lock) _userLimit = value; }
    }

    public IReadOnlyList<BanEntry> Bans
    {
        get { lock (_lock) return _bans.ToList(); }
    }

    public IReadOnlyList<string> InviteList
    {
        get { lock (_lock) return _invites.ToList(); }
    }

    public IReadOnlyList<BanEntry> Excepts
    {
        get { lock (_lock) return _excepts.ToList(); }
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

    public bool UpdateMemberNick(string oldNick, string newNick)
    {
        if (!_members.TryRemove(oldNick, out var membership))
            return false;
        membership.Nickname = newNick;
        _members[newNick] = membership;
        return true;
    }

    public ChannelMembership? GetMember(string nickname)
        => _members.TryGetValue(nickname, out var m) ? m : null;

    public bool IsMember(string nickname)
        => _members.ContainsKey(nickname);

    public bool AddBan(BanEntry ban, int maxBans = int.MaxValue)
    {
        lock (_lock)
        {
            if (_bans.Count >= maxBans) return false;
            _bans.Add(ban);
            return true;
        }
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
        lock (_lock) return _bans.Any(b => WildcardMatch(b.Mask, mask));
    }

    public void AddInvite(string nickname)
    {
        lock (_lock) _invites.Add(nickname);
    }

    public bool IsInvited(string nickname)
    {
        lock (_lock) return _invites.Contains(nickname);
    }

    public bool AddExcept(BanEntry except, int maxExcepts = int.MaxValue)
    {
        lock (_lock)
        {
            if (_excepts.Count >= maxExcepts) return false;
            _excepts.Add(except);
            return true;
        }
    }

    public bool RemoveExcept(string mask)
    {
        lock (_lock)
        {
            var idx = _excepts.FindIndex(e => string.Equals(e.Mask, mask, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) { _excepts.RemoveAt(idx); return true; }
            return false;
        }
    }

    public bool IsExcepted(string mask)
    {
        lock (_lock) return _excepts.Any(e => WildcardMatch(e.Mask, mask));
    }

    internal static bool WildcardMatch(string pattern, string input)
        => IrcWildcard.Match(pattern, input);
}
