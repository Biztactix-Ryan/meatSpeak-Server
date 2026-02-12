using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace MeatSpeak.Server.Tests;

public class RegistrationPipelineTests
{
    private readonly IServer _server;
    private readonly IEventBus _eventBus;
    private readonly DbWriteQueue _writeQueue;
    private readonly ServerMetrics _metrics;
    private readonly RegistrationPipeline _pipeline;
    private readonly ConcurrentDictionary<string, ISession> _sessions = new();

    public RegistrationPipelineTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig
        {
            ServerName = "test.server",
            NetworkName = "TestNet",
            Version = "test-1.0",
            Motd = new List<string> { "Welcome!" },
        });
        _server.StartedAt.Returns(DateTimeOffset.UtcNow);
        _server.ConnectionCount.Returns(1);
        _server.ChannelCount.Returns(0);
        _server.Sessions.Returns(_sessions);

        var modes = new ModeRegistry();
        modes.RegisterStandardModes();
        _server.Modes.Returns(modes);

        _eventBus = Substitute.For<IEventBus>();
        _server.Events.Returns(_eventBus);

        _writeQueue = new DbWriteQueue();
        _metrics = new ServerMetrics();

        var numerics = new NumericSender(_server);
        _pipeline = new RegistrationPipeline(_server, numerics, _writeQueue, NullLogger<RegistrationPipeline>.Instance, _metrics);
    }

    private ISession CreateSession(
        string? nick = "TestUser",
        string? user = "testuser",
        SessionState state = SessionState.Registering,
        bool capInNegotiation = false,
        bool capNegotiationComplete = false)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo
        {
            Nickname = nick,
            Username = user,
            Hostname = "host",
        };
        info.CapState.InNegotiation = capInNegotiation;
        info.CapState.NegotiationComplete = capNegotiationComplete;
        session.Info.Returns(info);
        session.Id.Returns("session-1");
        session.State.Returns(state);
        return session;
    }

    // --- No-op cases ---

    [Fact]
    public async Task TryCompleteRegistrationAsync_AlreadyRegistered_IsNoOp()
    {
        var session = CreateSession(state: SessionState.Registered);

        await _pipeline.TryCompleteRegistrationAsync(session);

        await session.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_MissingNickname_DoesNotRegister()
    {
        var session = CreateSession(nick: null);

        await _pipeline.TryCompleteRegistrationAsync(session);

        await session.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_MissingUsername_DoesNotRegister()
    {
        var session = CreateSession(user: null);

        await _pipeline.TryCompleteRegistrationAsync(session);

        await session.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_CapNegotiationInProgressAndNotComplete_DoesNotRegister()
    {
        var session = CreateSession(capInNegotiation: true, capNegotiationComplete: false);

        await _pipeline.TryCompleteRegistrationAsync(session);

        await session.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string[]>());
    }

    // --- Successful registration cases ---

    [Fact]
    public async Task TryCompleteRegistrationAsync_CapNegotiationComplete_Registers()
    {
        var session = CreateSession(capInNegotiation: true, capNegotiationComplete: true);

        await _pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME, Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_NoCapNegotiation_RegistersNormally()
    {
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME, Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_SetsStateToRegistered()
    {
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_SendsWelcomeNumerics()
    {
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        // Welcome sequence: RPL_WELCOME, RPL_YOURHOST, RPL_CREATED, RPL_MYINFO
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME, Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_YOURHOST, Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_CREATED, Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MYINFO, Arg.Any<string[]>());
        // ISUPPORT
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ISUPPORT, Arg.Any<string[]>());
        // LUSERS
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LUSERCLIENT, Arg.Any<string[]>());
        // MOTD
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MOTDSTART, Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFMOTD, Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_PublishesSessionRegisteredEvent()
    {
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        _eventBus.Received().Publish(Arg.Is<SessionRegisteredEvent>(e =>
            e.SessionId == "session-1" && e.Nickname == "TestUser"));
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_WritesUserHistoryToQueue()
    {
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        Assert.True(_writeQueue.Reader.TryRead(out var item));
        var history = Assert.IsType<AddUserHistory>(item);
        Assert.Equal("TestUser", history.Entity.Nickname);
        Assert.Equal("testuser", history.Entity.Username);
        Assert.Equal("host", history.Entity.Hostname);
    }
}
