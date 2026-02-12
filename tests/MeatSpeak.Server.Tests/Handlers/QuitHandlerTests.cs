using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Handlers.Connection;

namespace MeatSpeak.Server.Tests.Handlers;

public class QuitHandlerTests
{
    private ISession CreateSession(string nick, SessionState state = SessionState.Registered)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        session.State.Returns(state);
        return session;
    }

    [Fact]
    public async Task HandleAsync_WithReason_CallsDisconnectWithProvidedReason()
    {
        var handler = new QuitHandler();
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "QUIT", new[] { "Goodbye!" });

        await handler.HandleAsync(session, msg);

        await session.Received().DisconnectAsync("Goodbye!");
    }

    [Fact]
    public async Task HandleAsync_NoReason_DefaultsToClientQuit()
    {
        var handler = new QuitHandler();
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "QUIT", Array.Empty<string>());

        await handler.HandleAsync(session, msg);

        await session.Received().DisconnectAsync("Client quit");
    }

    [Fact]
    public async Task HandleAsync_Registered_WritesDisconnectToQueue()
    {
        var writeQueue = new DbWriteQueue();
        var handler = new QuitHandler(writeQueue);
        var session = CreateSession("TestUser", SessionState.Registered);
        var msg = new IrcMessage(null, null, "QUIT", new[] { "Leaving" });

        await handler.HandleAsync(session, msg);

        var success = writeQueue.Reader.TryRead(out var item);
        Assert.True(success);
        var disconnect = Assert.IsType<UpdateUserDisconnect>(item);
        Assert.Equal("TestUser", disconnect.Nickname);
        Assert.Equal("Leaving", disconnect.Reason);
    }

    [Fact]
    public async Task HandleAsync_NotRegistered_DoesNotWriteToQueue()
    {
        var writeQueue = new DbWriteQueue();
        var handler = new QuitHandler(writeQueue);
        var session = CreateSession("TestUser", SessionState.Connecting);
        var msg = new IrcMessage(null, null, "QUIT", new[] { "Leaving" });

        await handler.HandleAsync(session, msg);

        var success = writeQueue.Reader.TryRead(out _);
        Assert.False(success);
    }

    [Fact]
    public async Task HandleAsync_NullDbWriteQueue_StillDisconnects()
    {
        var handler = new QuitHandler(null);
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "QUIT", new[] { "Bye" });

        await handler.HandleAsync(session, msg);

        await session.Received().DisconnectAsync("Bye");
    }
}
