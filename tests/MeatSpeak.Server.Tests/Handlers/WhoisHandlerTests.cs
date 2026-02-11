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

public class WhoisHandlerTests
{
    private readonly IServer _server;
    private readonly WhoisHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public WhoisHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig
        {
            ServerName = "test.server",
            Description = "Test Server"
        });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new WhoisHandler(_server);
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
    public async Task HandleAsync_NoParams_SendsNeedMoreParams()
    {
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOIS", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NONICKNAMEGIVEN,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoSuchNick_SendsError()
    {
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOIS", new[] { "NonExistent" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHNICK,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFWHOIS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ValidWhois_SendsAllReplies()
    {
        var target = CreateSession("Alice");
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOIS", new[] { "Alice" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WHOISUSER,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WHOISSERVER,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFWHOIS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WhoisOperator_SendsOperReply()
    {
        var target = CreateSession("Alice");
        target.Info.UserModes.Add('o');
        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOIS", new[] { "Alice" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WHOISOPERATOR,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WhoisWithChannels_SendsChannelList()
    {
        var target = CreateSession("Alice");
        target.Info.Channels.Add("#test");
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOIS", new[] { "Alice" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WHOISCHANNELS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WhoisSecretChannel_HidesFromNonMember()
    {
        var target = CreateSession("Alice");
        target.Info.Channels.Add("#secret");
        var channel = new ChannelImpl("#secret");
        channel.Modes.Add('s');
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice" });
        _channels["#secret"] = channel;

        var session = CreateSession("Querier");
        var msg = new IrcMessage(null, null, "WHOIS", new[] { "Alice" });

        await _handler.HandleAsync(session, msg);

        // Should NOT receive RPL_WHOISCHANNELS since the only channel is secret
        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.RPL_WHOISCHANNELS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_TwoParams_UsesSecondAsNick()
    {
        var target = CreateSession("Alice");
        var session = CreateSession("Querier");
        // WHOIS server nick
        var msg = new IrcMessage(null, null, "WHOIS", new[] { "some.server", "Alice" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WHOISUSER,
            Arg.Any<string[]>());
    }
}
