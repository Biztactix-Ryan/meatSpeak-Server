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

    public void AddExcept(BanEntry except)
    {
        lock (_lock) _excepts.Add(except);
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

    /// <summary>
    /// Matches an IRC wildcard pattern against an input string.
    /// Supports '*' (zero or more characters) and '?' (exactly one character).
    /// Comparison is case-insensitive.
    /// </summary>
    internal static bool WildcardMatch(string pattern, string input)
    {
        // Iterative two-pointer algorithm with backtracking for '*'
        int pIdx = 0, iIdx = 0;
        int starIdx = -1, matchIdx = 0;

        while (iIdx < input.Length)
        {
            if (pIdx < pattern.Length &&
                (char.ToLowerInvariant(pattern[pIdx]) == char.ToLowerInvariant(input[iIdx]) ||
                 pattern[pIdx] == '?'))
            {
                // Characters match or pattern has '?': advance both pointers
                pIdx++;
                iIdx++;
            }
            else if (pIdx < pattern.Length && pattern[pIdx] == '*')
            {
                // '*' found: record position and try matching zero characters
                starIdx = pIdx;
                matchIdx = iIdx;
                pIdx++;
            }
            else if (starIdx >= 0)
            {
                // Mismatch but we have a previous '*': backtrack
                // Let the '*' consume one more character from input
                pIdx = starIdx + 1;
                matchIdx++;
                iIdx = matchIdx;
            }
            else
            {
                // Mismatch with no '*' to backtrack to
                return false;
            }
        }

        // Consume any trailing '*' characters in the pattern
        while (pIdx < pattern.Length && pattern[pIdx] == '*')
        {
            pIdx++;
        }

        return pIdx == pattern.Length;
    }
}
