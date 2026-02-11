using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Operator;

namespace MeatSpeak.Server.Tests.Handlers;

public class OperHandlerTests
{
    private readonly IServer _server;
    private readonly OperHandler _handler;

    public OperHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig
        {
            ServerName = "test.server",
            OperName = "admin",
            OperPassword = "secret"
        });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _handler = new OperHandler(_server);
    }

    private ISession CreateSession(string nick)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        session.Info.Returns(info);
        return session;
    }

    [Fact]
    public async Task HandleAsync_NoParams_SendsNeedMoreParams()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "OPER", new[] { "admin" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WrongPassword_SendsError()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "OPER", new[] { "admin", "wrongpass" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_PASSWDMISMATCH,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WrongName_SendsError()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "OPER", new[] { "wrongname", "secret" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_PASSWDMISMATCH,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ValidCredentials_SetsOperMode()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "OPER", new[] { "admin", "secret" });

        await _handler.HandleAsync(session, msg);

        Assert.Contains('o', session.Info.UserModes);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_YOUREOPER,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoOperConfigured_SendsNoOperHost()
    {
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        var handler = new OperHandler(_server);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "OPER", new[] { "admin", "secret" });

        await handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOOPERHOST,
            Arg.Any<string[]>());
    }
}
