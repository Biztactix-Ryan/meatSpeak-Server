using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Diagnostics;
using Microsoft.Extensions.Logging;

namespace MeatSpeak.Server.Tests;

public class PingTimeoutServiceTests
{
    private readonly IServer _server;
    private readonly ServerMetrics _metrics;
    private readonly Dictionary<string, ISession> _sessions;

    public PingTimeoutServiceTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig
        {
            ServerName = "test.server",
            PingInterval = 60,
            PingTimeout = 180,
        });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _metrics = new ServerMetrics();
        _sessions = new Dictionary<string, ISession>();
        _server.Sessions.Returns(_sessions);
    }

    private ISession CreateSession(string nick, DateTimeOffset lastActivity, SessionState state = SessionState.Registered)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo
        {
            Nickname = nick,
            Username = "user",
            Hostname = "host",
            LastActivity = lastActivity,
        };
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        session.State.Returns(state);
        _sessions[session.Id] = session;
        return session;
    }

    private PingTimeoutService CreateService()
    {
        var logger = Substitute.For<ILogger<PingTimeoutService>>();
        return new PingTimeoutService(_server, logger, _metrics);
    }

    [Fact]
    public async Task ActiveSession_NoPingOrDisconnect()
    {
        // Session active 10 seconds ago — well within ping interval (60s)
        var session = CreateSession("Active", DateTimeOffset.UtcNow.AddSeconds(-10));
        var service = CreateService();

        await service.CheckSessionsAsync();

        // Active session should not receive PING or be disconnected
        await session.DidNotReceive().SendMessageAsync(null, "PING", Arg.Any<string[]>());
        await session.DidNotReceive().DisconnectAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task IdleSession_ReceivesPing()
    {
        // Session idle for 90 seconds — past PingInterval(60) but within PingTimeout(180)
        var session = CreateSession("Idle", DateTimeOffset.UtcNow.AddSeconds(-90));
        var service = CreateService();

        await service.CheckSessionsAsync();

        // Should have sent a PING
        await session.Received().SendMessageAsync(null, "PING", Arg.Any<string[]>());
        // Should NOT have disconnected
        await session.DidNotReceive().DisconnectAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task TimedOutSession_IsDisconnected()
    {
        // Session idle for 200 seconds — past PingTimeout(180)
        var session = CreateSession("TimedOut", DateTimeOffset.UtcNow.AddSeconds(-200));
        var service = CreateService();

        await service.CheckSessionsAsync();

        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("Ping timeout")));
        Assert.True(_metrics.PingTimeouts > 0);
    }

    [Fact]
    public async Task DisconnectingSession_IsSkipped()
    {
        var session = CreateSession("Disconnecting", DateTimeOffset.UtcNow.AddSeconds(-200),
            state: SessionState.Disconnecting);
        var service = CreateService();

        await service.CheckSessionsAsync();

        await session.DidNotReceive().SendMessageAsync(null, "PING", Arg.Any<string[]>());
        await session.DidNotReceive().DisconnectAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task UnregisteredSession_NoPingSentButCanTimeout()
    {
        // Unregistered session (still in CAP negotiation) that's idle but within timeout
        var session = CreateSession("Unregistered", DateTimeOffset.UtcNow.AddSeconds(-90),
            state: SessionState.Registering);
        var service = CreateService();

        await service.CheckSessionsAsync();

        // Should NOT send PING to unregistered sessions (they haven't completed handshake)
        await session.DidNotReceive().SendMessageAsync(null, "PING", Arg.Any<string[]>());
        // Should NOT disconnect — within 180s timeout
        await session.DidNotReceive().DisconnectAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task UnregisteredSession_StillTimesOut()
    {
        // Unregistered session that has exceeded timeout
        var session = CreateSession("Unregistered", DateTimeOffset.UtcNow.AddSeconds(-200),
            state: SessionState.Registering);
        var service = CreateService();

        await service.CheckSessionsAsync();

        // Should still disconnect if past timeout, even if unregistered
        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("Ping timeout")));
    }

    [Fact]
    public async Task MultipleSessionsMixedStates_HandledCorrectly()
    {
        var active = CreateSession("Active", DateTimeOffset.UtcNow.AddSeconds(-10));
        var idle = CreateSession("Idle", DateTimeOffset.UtcNow.AddSeconds(-90));
        var timedOut = CreateSession("TimedOut", DateTimeOffset.UtcNow.AddSeconds(-200));
        var disconnecting = CreateSession("Disc", DateTimeOffset.UtcNow.AddSeconds(-200),
            state: SessionState.Disconnecting);

        var service = CreateService();
        await service.CheckSessionsAsync();

        // Active: nothing
        await active.DidNotReceive().SendMessageAsync(null, "PING", Arg.Any<string[]>());
        await active.DidNotReceive().DisconnectAsync(Arg.Any<string>());

        // Idle: PING only
        await idle.Received().SendMessageAsync(null, "PING", Arg.Any<string[]>());
        await idle.DidNotReceive().DisconnectAsync(Arg.Any<string>());

        // TimedOut: disconnect
        await timedOut.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("Ping timeout")));

        // Disconnecting: skipped
        await disconnecting.DidNotReceive().DisconnectAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task PingTimeout_IncrementsMetric()
    {
        CreateSession("TimedOut1", DateTimeOffset.UtcNow.AddSeconds(-200));
        CreateSession("TimedOut2", DateTimeOffset.UtcNow.AddSeconds(-300));
        var service = CreateService();

        await service.CheckSessionsAsync();

        Assert.Equal(2, _metrics.PingTimeouts);
    }

    [Fact]
    public async Task PingSendsServerName()
    {
        var session = CreateSession("Idle", DateTimeOffset.UtcNow.AddSeconds(-90));
        var service = CreateService();

        await service.CheckSessionsAsync();

        await session.Received().SendMessageAsync(null, "PING",
            Arg.Is<string[]>(p => p.Length > 0 && p[0] == "test.server"));
    }
}
