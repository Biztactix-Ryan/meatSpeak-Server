using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Channels;
using MeatSpeak.Server.State;

namespace MeatSpeak.Server.Tests.Handlers;

public class WhoHandlerTests
{
    private readonly IServer _server;
    private readonly WhoHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public WhoHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new WhoHandler(_server);
    }

    private ISession CreateSession(string nick)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host", Realname = "Real Name" };
        session.Info.Returns(info);
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task HandleAsync_ChannelWho_SendsWhoReplies()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice", IsOperator = true });
        channel.AddMember("Bob", new ChannelMembership { Nickname = "Bob" });
        _channels["#test"] = channel;

        CreateSession("Alice");
        CreateSession("Bob");
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHO", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received(2).SendNumericAsync("test.server", IrcNumerics.RPL_WHOREPLY,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFWHO,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NickWho_SendsWhoReply()
    {
        var targetSession = CreateSession("Alice");
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHO", new[] { "Alice" });

        await _handler.HandleAsync(session, msg);

        await session.Received(1).SendNumericAsync("test.server", IrcNumerics.RPL_WHOREPLY,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFWHO,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NonexistentNick_SendsOnlyEndOfWho()
    {
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHO", new[] { "NoSuch" });

        await _handler.HandleAsync(session, msg);

        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.RPL_WHOREPLY,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFWHO,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoParams_SendsEndOfWho()
    {
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHO", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFWHO,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_AwayNickWho_ShowsGoneFlag()
    {
        var targetSession = CreateSession("Alice");
        targetSession.Info.AwayMessage = "Away";
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHO", new[] { "Alice" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WHOREPLY,
            Arg.Is<string[]>(p => p[5] == "G"));
    }

    [Fact]
    public async Task HandleAsync_HereNickWho_ShowsHereFlag()
    {
        var targetSession = CreateSession("Alice");
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHO", new[] { "Alice" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WHOREPLY,
            Arg.Is<string[]>(p => p[5] == "H"));
    }

    [Fact]
    public async Task HandleAsync_AwayChannelWho_ShowsGoneFlag()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice" });
        _channels["#test"] = channel;

        var aliceSession = CreateSession("Alice");
        aliceSession.Info.AwayMessage = "BRB";
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHO", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WHOREPLY,
            Arg.Is<string[]>(p => p[5].StartsWith("G")));
    }
}
