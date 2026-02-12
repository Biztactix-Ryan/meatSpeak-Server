namespace MeatSpeak.Server.State;

using System.Collections.Concurrent;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Diagnostics;

public sealed class ServerState : IServer
{
    private readonly ConcurrentDictionary<string, ISession> _sessions = new();
    private readonly ConcurrentDictionary<string, ISession> _nickIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, IChannel> _channels = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, LinkedList<WhowasEntry>> _whowas = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxWhowasEntries = 100;
    private readonly ServerMetrics? _metrics;

    public ServerConfig Config { get; }
    public CommandRegistry Commands { get; }
    public ModeRegistry Modes { get; }
    public CapabilityRegistry Capabilities { get; }
    public IEventBus Events { get; }
    public DateTimeOffset StartedAt { get; } = DateTimeOffset.UtcNow;

    public ServerState(
        ServerConfig config,
        CommandRegistry commands,
        ModeRegistry modes,
        CapabilityRegistry capabilities,
        IEventBus events,
        ServerMetrics? metrics = null)
    {
        Config = config;
        Commands = commands;
        Modes = modes;
        Capabilities = capabilities;
        Events = events;
        _metrics = metrics;
    }

    public IReadOnlyDictionary<string, ISession> Sessions => _sessions;
    public int ConnectionCount => _sessions.Count;
    public int ChannelCount => _channels.Count;

    public void AddSession(ISession session) => _sessions[session.Id] = session;

    public void RemoveSession(string sessionId)
    {
        if (_sessions.TryRemove(sessionId, out var session))
        {
            var nick = session.Info.Nickname;
            if (nick != null)
                _nickIndex.TryRemove(nick, out _);
        }
    }

    public ISession? FindSessionByNick(string nickname)
    {
        var start = ServerMetrics.GetTimestamp();
        try
        {
            _nickIndex.TryGetValue(nickname, out var session);
            return session;
        }
        finally
        {
            _metrics?.RecordNickLookupDuration(ServerMetrics.GetElapsedMs(start));
        }
    }

    public void UpdateNickIndex(string? oldNick, string? newNick, ISession session)
    {
        if (oldNick != null)
            _nickIndex.TryRemove(oldNick, out _);
        if (newNick != null)
            _nickIndex[newNick] = session;
    }

    public bool TryClaimNick(string newNick, ISession session)
    {
        return _nickIndex.TryAdd(newNick, session);
    }

    public IReadOnlyDictionary<string, IChannel> Channels => _channels;

    public IChannel GetOrCreateChannel(string name)
    {
        return _channels.GetOrAdd(name, n => new ChannelImpl(n));
    }

    public void RemoveChannel(string name) => _channels.TryRemove(name, out _);

    public void RecordWhowas(WhowasEntry entry)
    {
        var list = _whowas.GetOrAdd(entry.Nickname, _ => new LinkedList<WhowasEntry>());
        lock (list)
        {
            list.AddFirst(entry);
            while (list.Count > MaxWhowasEntries)
                list.RemoveLast();
        }

        // Bound the total number of tracked nicknames
        var maxNicks = Config.MaxWhowasNicknames;
        if (_whowas.Count > maxNicks)
        {
            foreach (var key in _whowas.Keys)
            {
                if (string.Equals(key, entry.Nickname, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (_whowas.TryRemove(key, out _))
                    break;
            }
        }
    }

    public IReadOnlyList<WhowasEntry> GetWhowas(string nickname, int maxCount = 10)
    {
        if (!_whowas.TryGetValue(nickname, out var list))
            return Array.Empty<WhowasEntry>();
        lock (list)
        {
            return list.Take(maxCount).ToList();
        }
    }
}
