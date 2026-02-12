using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Handlers.Connection;

namespace MeatSpeak.Server.Tests.Handlers;

public class PongHandlerTests
{
    [Fact]
    public async Task HandleAsync_UpdatesLastActivity()
    {
        var handler = new PongHandler();
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        var before = info.LastActivity;
        session.Info.Returns(info);

        var msg = new IrcMessage(null, null, "PONG", new[] { "token" });

        await handler.HandleAsync(session, msg);

        Assert.True(info.LastActivity >= before);
    }

    [Fact]
    public async Task HandleAsync_LastActivityIsApproximatelyNow()
    {
        var handler = new PongHandler();
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        // Set LastActivity to an old value to verify it gets updated
        info.LastActivity = DateTimeOffset.UtcNow.AddMinutes(-10);
        session.Info.Returns(info);

        var msg = new IrcMessage(null, null, "PONG", new[] { "token" });
        var now = DateTimeOffset.UtcNow;

        await handler.HandleAsync(session, msg);

        var diff = info.LastActivity - now;
        Assert.True(Math.Abs(diff.TotalSeconds) < 2);
    }
}
