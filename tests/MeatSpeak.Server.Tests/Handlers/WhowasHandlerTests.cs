using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Channels;

namespace MeatSpeak.Server.Tests.Handlers;

public class WhowasHandlerTests
{
    private readonly IServer _server;
    private readonly WhowasHandler _handler;

    public WhowasHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig
        {
            ServerName = "test.server",
            Description = "Test Server"
        });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _server.GetWhowas(Arg.Any<string>(), Arg.Any<int>())
            .Returns(Array.Empty<WhowasEntry>());
        _handler = new WhowasHandler(_server);
    }

    private ISession CreateSession(string nick)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        session.Info.Returns(info);
        return session;
    }

    [Fact]
    public async Task HandleAsync_NoParams_SendsNoNicknameGiven()
    {
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOWAS", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NONICKNAMEGIVEN,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoHistory_SendsWasNoSuchNick()
    {
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOWAS", new[] { "Unknown" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_WASNOSUCHNICK,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFWHOWAS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WithHistory_SendsWhowasEntries()
    {
        var entries = new List<WhowasEntry>
        {
            new("Alice", "alice", "host1.example.com", "Alice Smith", DateTimeOffset.UtcNow),
            new("Alice", "alice2", "host2.example.com", "Alice Jones", DateTimeOffset.UtcNow.AddMinutes(-5)),
        };
        _server.GetWhowas("Alice", Arg.Any<int>()).Returns(entries);

        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOWAS", new[] { "Alice" });

        await _handler.HandleAsync(session, msg);

        await session.Received(2).SendNumericAsync("test.server", IrcNumerics.RPL_WHOWASUSER,
            Arg.Any<string[]>());
        await session.Received(2).SendNumericAsync("test.server", IrcNumerics.RPL_WHOISSERVER,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFWHOWAS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WithHistory_DoesNotSendWasNoSuchNick()
    {
        var entries = new List<WhowasEntry>
        {
            new("Alice", "alice", "host.example.com", "Alice", DateTimeOffset.UtcNow),
        };
        _server.GetWhowas("Alice", Arg.Any<int>()).Returns(entries);

        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOWAS", new[] { "Alice" });

        await _handler.HandleAsync(session, msg);

        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.ERR_WASNOSUCHNICK,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WithCountParam_PassesCountToServer()
    {
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOWAS", new[] { "Alice", "5" });

        await _handler.HandleAsync(session, msg);

        _server.Received().GetWhowas("Alice", 5);
    }

    [Fact]
    public async Task HandleAsync_AlwaysSendsEndOfWhowas()
    {
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOWAS", new[] { "Anyone" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFWHOWAS,
            Arg.Any<string[]>());
    }
}
