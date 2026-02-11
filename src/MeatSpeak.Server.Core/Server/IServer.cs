namespace MeatSpeak.Server.Core.Server;

using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Events;

public interface IServer
{
    ServerConfig Config { get; }
    CommandRegistry Commands { get; }
    ModeRegistry Modes { get; }
    CapabilityRegistry Capabilities { get; }
    IEventBus Events { get; }

    // Session management
    IReadOnlyDictionary<string, ISession> Sessions { get; }
    void AddSession(ISession session);
    void RemoveSession(string sessionId);
    ISession? FindSessionByNick(string nickname);

    // Channel management
    IReadOnlyDictionary<string, IChannel> Channels { get; }
    IChannel GetOrCreateChannel(string name);
    void RemoveChannel(string name);

    // Stats
    int ConnectionCount { get; }
    int ChannelCount { get; }
    DateTimeOffset StartedAt { get; }
}
