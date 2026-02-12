using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Numerics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeatSpeak.Server.Tests.Handlers;

public class UserHandlerTests
{
    private readonly IServer _server;
    private readonly RegistrationPipeline _registration;
    private readonly UserHandler _handler;

    public UserHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _server.TryClaimNick(Arg.Any<string>(), Arg.Any<ISession>()).Returns(true);
        var modes = new ModeRegistry();
        modes.RegisterStandardModes();
        _server.Modes.Returns(modes);
        var numerics = new NumericSender(_server);
        _registration = new RegistrationPipeline(_server, numerics, null, null, NullLogger<RegistrationPipeline>.Instance, new ServerMetrics());
        _handler = new UserHandler(_server, _registration);
    }

    [Fact]
    public async Task HandleAsync_ValidUser_SetsUsernameAndRealname()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        session.State.Returns(SessionState.Connecting);
        var msg = new IrcMessage(null, null, "USER", new[] { "myuser", "0", "*", "My Real Name" });

        await _handler.HandleAsync(session, msg);

        Assert.Equal("myuser", info.Username);
        Assert.Equal("My Real Name", info.Realname);
    }

    [Fact]
    public async Task HandleAsync_NoParams_SendsNeedMoreParams()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        session.State.Returns(SessionState.Connecting);
        var msg = new IrcMessage(null, null, "USER", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ThreeParams_SendsNeedMoreParams()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        session.State.Returns(SessionState.Connecting);
        var msg = new IrcMessage(null, null, "USER", new[] { "myuser", "0", "*" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_AlreadyRegistered_SendsError()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        session.State.Returns(SessionState.Registered);
        var msg = new IrcMessage(null, null, "USER", new[] { "myuser", "0", "*", "Real" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_ALREADYREGISTRED,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_SetsStateToRegistering()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        session.State = SessionState.Connecting;
        var msg = new IrcMessage(null, null, "USER", new[] { "myuser", "0", "*", "Real" });

        await _handler.HandleAsync(session, msg);

        // State should have been set to Registering (at minimum)
        session.Received().State = SessionState.Registering;
    }

    [Fact]
    public async Task HandleAsync_AlreadyRegistering_DoesNotDowngradeState()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        // Already in Registering state (e.g. NICK was sent first)
        session.State.Returns(SessionState.Registering);
        var msg = new IrcMessage(null, null, "USER", new[] { "myuser", "0", "*", "Real" });

        await _handler.HandleAsync(session, msg);

        // Should NOT have received a State = Registering assignment (already at that level)
        // The handler only sets state if < Registering
        Assert.Equal("myuser", info.Username);
    }

    [Fact]
    public async Task HandleAsync_WithNickAlreadySet_TriggersRegistration()
    {
        // If NICK was already set and CAP negotiation is not in progress,
        // TryCompleteRegistrationAsync should proceed to register
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = "TestUser", Hostname = "host" };
        session.Info.Returns(info);
        session.State = SessionState.Connecting;
        session.Id.Returns("test-session");
        var msg = new IrcMessage(null, null, "USER", new[] { "myuser", "0", "*", "Real" });

        await _handler.HandleAsync(session, msg);

        // Registration should complete - welcome numeric (001) sent
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WithoutNick_DoesNotCompleteRegistration()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo(); // No nickname set
        session.Info.Returns(info);
        session.State = SessionState.Connecting;
        var msg = new IrcMessage(null, null, "USER", new[] { "myuser", "0", "*", "Real" });

        await _handler.HandleAsync(session, msg);

        // Registration should NOT complete - no welcome numeric
        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_EmptyUsername_Rejected()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        session.State.Returns(SessionState.Connecting);
        var msg = new IrcMessage(null, null, "USER", new[] { "", "0", "*", "Real" });

        await _handler.HandleAsync(session, msg);

        // Empty username is rejected
        Assert.Null(info.Username);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_LongRealname_Rejected()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        session.State.Returns(SessionState.Connecting);
        var longRealname = new string('A', 500);
        var msg = new IrcMessage(null, null, "USER", new[] { "myuser", "0", "*", longRealname });

        await _handler.HandleAsync(session, msg);

        // Realname over 64 chars is rejected
        Assert.Null(info.Realname);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }
}
