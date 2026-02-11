namespace MeatSpeak.Server.State;

using System.Collections.Concurrent;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;

public sealed class ServerState : IServer
{
    private readonly ConcurrentDictionary<string, ISession> _sessions = new();
    private readonly ConcurrentDictionary<string, IChannel> _channels = new(StringComparer.OrdinalIgnoreCase);

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
        IEventBus events)
    {
        Config = config;
        Commands = commands;
        Modes = modes;
        Capabilities = capabilities;
        Events = events;
    }

    public IReadOnlyDictionary<string, ISession> Sessions => _sessions;
    public int ConnectionCount => _sessions.Count;
    public int ChannelCount => _channels.Count;

    public void AddSession(ISession session) => _sessions[session.Id] = session;

    public void RemoveSession(string sessionId) => _sessions.TryRemove(sessionId, out _);

    public ISession? FindSessionByNick(string nickname)
    {
        foreach (var session in _sessions.Values)
            if (string.Equals(session.Info.Nickname, nickname, StringComparison.OrdinalIgnoreCase))
                return session;
        return null;
    }

    public IReadOnlyDictionary<string, IChannel> Channels => _channels;

    public IChannel GetOrCreateChannel(string name)
    {
        return _channels.GetOrAdd(name, n => new ChannelImpl(n));
    }

    public void RemoveChannel(string name) => _channels.TryRemove(name, out _);
}
