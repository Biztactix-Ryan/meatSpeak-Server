using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Connection;

namespace MeatSpeak.Server.Tests.Handlers;

public class AwayHandlerTests
{
    private readonly IServer _server;
    private readonly AwayHandler _handler;

    public AwayHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _handler = new AwayHandler(_server);
    }

    private ISession CreateSession(string nick)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        session.Info.Returns(info);
        return session;
    }

    [Fact]
    public async Task HandleAsync_WithMessage_SetsAwayAndSendsNowAway()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "AWAY", new[] { "Gone fishing" });

        await _handler.HandleAsync(session, msg);

        Assert.Equal("Gone fishing", session.Info.AwayMessage);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NOWAWAY,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoMessage_ClearsAwayAndSendsUnaway()
    {
        var session = CreateSession("TestUser");
        session.Info.AwayMessage = "Previously away";
        var msg = new IrcMessage(null, null, "AWAY", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        Assert.Null(session.Info.AwayMessage);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_UNAWAY,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_EmptyMessage_ClearsAway()
    {
        var session = CreateSession("TestUser");
        session.Info.AwayMessage = "Was away";
        var msg = new IrcMessage(null, null, "AWAY", new[] { "" });

        await _handler.HandleAsync(session, msg);

        Assert.Null(session.Info.AwayMessage);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_UNAWAY,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChangeAwayMessage_UpdatesMessage()
    {
        var session = CreateSession("TestUser");
        session.Info.AwayMessage = "Old message";

        var msg = new IrcMessage(null, null, "AWAY", new[] { "New message" });
        await _handler.HandleAsync(session, msg);

        Assert.Equal("New message", session.Info.AwayMessage);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NOWAWAY,
            Arg.Any<string[]>());
    }
}
