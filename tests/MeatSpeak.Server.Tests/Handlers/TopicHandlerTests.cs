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

public class TopicHandlerTests
{
    private readonly IServer _server;
    private readonly IEventBus _events;
    private readonly TopicHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public TopicHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _events = Substitute.For<IEventBus>();
        _server.Events.Returns(_events);
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new TopicHandler(_server);
    }

    private ISession CreateSession(string nick)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task HandleAsync_NoParams_SendsNeedMoreParams()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "TOPIC", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoSuchChannel_SendsError()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#nonexistent" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHCHANNEL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NotOnChannel_SendsError()
    {
        var channel = new ChannelImpl("#test");
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOTONCHANNEL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_QueryTopicSet_SendsRplTopic()
    {
        var channel = new ChannelImpl("#test");
        channel.Topic = "Hello World";
        channel.TopicSetBy = "admin";
        channel.TopicSetAt = DateTimeOffset.UtcNow;
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_TOPIC,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_TOPICWHOTIME,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_QueryNoTopic_SendsRplNoTopic()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NOTOPIC,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_SetTopicWithOp_SetsTopic()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#test", "New Topic" });

        await _handler.HandleAsync(session, msg);

        Assert.Equal("New Topic", channel.Topic);
        Assert.Equal("TestUser", channel.TopicSetBy);
        Assert.NotNull(channel.TopicSetAt);
    }

    [Fact]
    public async Task HandleAsync_SetTopicProtectedWithoutOp_SendsError()
    {
        var channel = new ChannelImpl("#test"); // +t is default
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = false });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#test", "New Topic" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_CHANOPRIVSNEEDED,
            Arg.Any<string[]>());
        Assert.Null(channel.Topic);
    }

    [Fact]
    public async Task HandleAsync_SetTopicNoProtection_AnyoneCanSet()
    {
        var channel = new ChannelImpl("#test");
        channel.Modes.Remove('t'); // Remove topic protection
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = false });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#test", "New Topic" });

        await _handler.HandleAsync(session, msg);

        Assert.Equal("New Topic", channel.Topic);
    }

    [Fact]
    public async Task HandleAsync_SetTopic_BroadcastsToMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = true });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var otherSession = CreateSession("Other");
        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#test", "New Topic" });

        await _handler.HandleAsync(session, msg);

        await otherSession.Received().SendMessageAsync(
            Arg.Any<string>(), "TOPIC", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_SetTopic_PublishesEvent()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#test", "New Topic" });

        await _handler.HandleAsync(session, msg);

        _events.Received().Publish(Arg.Is<TopicChangedEvent>(e =>
            e.Channel == "#test" && e.Topic == "New Topic"));
    }
}
