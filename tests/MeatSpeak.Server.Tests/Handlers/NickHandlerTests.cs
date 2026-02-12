using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Numerics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeatSpeak.Server.Tests.Handlers;

public class NickHandlerTests
{
    private readonly IServer _server;
    private readonly RegistrationPipeline _registration;
    private readonly NickHandler _handler;

    public NickHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _server.TryClaimNick(Arg.Any<string>(), Arg.Any<ISession>()).Returns(true);
        var numerics = new NumericSender(_server);
        _registration = new RegistrationPipeline(_server, numerics, null, NullLogger<RegistrationPipeline>.Instance, new ServerMetrics());
        _handler = new NickHandler(_server, _registration);
    }

    [Fact]
    public async Task HandleAsync_ValidNick_SetsNickname()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        session.State.Returns(SessionState.Connecting);
        var msg = new IrcMessage(null, null, "NICK", new[] { "TestUser" });

        await _handler.HandleAsync(session, msg);

        Assert.Equal("TestUser", info.Nickname);
    }

    [Fact]
    public async Task HandleAsync_NoNick_SendsError()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "NICK", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", 431, Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NickInUse_SendsError()
    {
        _server.TryClaimNick("TakenNick", Arg.Any<ISession>()).Returns(false);

        var session = Substitute.For<ISession>();
        session.Id.Returns("my-session");
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "NICK", new[] { "TakenNick" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", 433, Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_InvalidNick_SendsError()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "NICK", new[] { "123invalid" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", 432, Arg.Any<string[]>());
    }
}
