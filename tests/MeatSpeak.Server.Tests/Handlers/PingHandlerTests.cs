using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Handlers.Connection;

namespace MeatSpeak.Server.Tests.Handlers;

public class PingHandlerTests
{
    [Fact]
    public async Task HandleAsync_SendsPong()
    {
        var handler = new PingHandler();
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "PING", new[] { "token123" });

        await handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync(null, "PONG", Arg.Is<string[]>(p => p[0] == "token123"));
    }

    [Fact]
    public void Command_IsPing()
    {
        var handler = new PingHandler();
        Assert.Equal("PING", handler.Command);
    }

    [Fact]
    public void MinimumState_IsConnecting()
    {
        var handler = new PingHandler();
        Assert.Equal(SessionState.Connecting, handler.MinimumState);
    }
}
