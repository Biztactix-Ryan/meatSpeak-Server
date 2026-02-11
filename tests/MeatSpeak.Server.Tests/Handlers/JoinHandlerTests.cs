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

public class JoinHandlerTests
{
    private readonly IServer _server;
    private readonly IEventBus _events;
    private readonly JoinHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public JoinHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _events = Substitute.For<IEventBus>();
        _server.Events.Returns(_events);
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new JoinHandler(_server);
    }

    private ISession CreateSession(string nick, string user = "user", string host = "host")
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = user, Hostname = host };
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task HandleAsync_NoParams_SendsNeedMoreParams()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_InvalidChannelName_SendsNoSuchChannel()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "nochanprefix" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHCHANNEL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NewChannel_CreatesAndJoinsWithOp()
    {
        var session = CreateSession("TestUser");
        var channel = new ChannelImpl("#test");
        _server.GetOrCreateChannel("#test").Returns(channel);
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        Assert.True(channel.IsMember("TestUser"));
        Assert.True(channel.GetMember("TestUser")!.IsOperator);
        Assert.Contains("#test", session.Info.Channels);
        await session.Received().SendMessageAsync(
            Arg.Any<string>(), "JOIN", Arg.Is<string[]>(p => p[0] == "#test"));
    }

    [Fact]
    public async Task HandleAsync_ExistingChannel_JoinsWithoutOp()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Existing", new ChannelMembership { Nickname = "Existing", IsOperator = true });
        _channels["#test"] = channel;
        _server.GetOrCreateChannel("#test").Returns(channel);

        var existingSession = CreateSession("Existing");
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        Assert.True(channel.IsMember("TestUser"));
        Assert.False(channel.GetMember("TestUser")!.IsOperator);
    }

    [Fact]
    public async Task HandleAsync_AlreadyMember_DoesNothing()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        _channels["#test"] = channel;
        _server.GetOrCreateChannel("#test").Returns(channel);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), "JOIN", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WrongKey_SendsBadChannelKey()
    {
        var channel = new ChannelImpl("#secret");
        channel.Key = "correctkey";
        _channels["#secret"] = channel;
        _server.GetOrCreateChannel("#secret").Returns(channel);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#secret", "wrongkey" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_BADCHANNELKEY,
            Arg.Any<string[]>());
        Assert.False(channel.IsMember("TestUser"));
    }

    [Fact]
    public async Task HandleAsync_CorrectKey_Joins()
    {
        var channel = new ChannelImpl("#secret");
        channel.Key = "mykey";
        _channels["#secret"] = channel;
        _server.GetOrCreateChannel("#secret").Returns(channel);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#secret", "mykey" });

        await _handler.HandleAsync(session, msg);

        Assert.True(channel.IsMember("TestUser"));
    }

    [Fact]
    public async Task HandleAsync_InviteOnly_NotInvited_SendsError()
    {
        var channel = new ChannelImpl("#invite");
        channel.Modes.Add('i');
        _channels["#invite"] = channel;
        _server.GetOrCreateChannel("#invite").Returns(channel);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#invite" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_INVITEONLYCHAN,
            Arg.Any<string[]>());
        Assert.False(channel.IsMember("TestUser"));
    }

    [Fact]
    public async Task HandleAsync_InviteOnly_Invited_Joins()
    {
        var channel = new ChannelImpl("#invite");
        channel.Modes.Add('i');
        channel.AddInvite("TestUser");
        _channels["#invite"] = channel;
        _server.GetOrCreateChannel("#invite").Returns(channel);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#invite" });

        await _handler.HandleAsync(session, msg);

        Assert.True(channel.IsMember("TestUser"));
    }

    [Fact]
    public async Task HandleAsync_Banned_SendsError()
    {
        var channel = new ChannelImpl("#test");
        channel.AddBan(new BanEntry("TestUser!user@host", "op", DateTimeOffset.UtcNow));
        _channels["#test"] = channel;
        _server.GetOrCreateChannel("#test").Returns(channel);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_BANNEDFROMCHAN,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_BannedButExcepted_Joins()
    {
        var channel = new ChannelImpl("#test");
        channel.AddBan(new BanEntry("TestUser!user@host", "op", DateTimeOffset.UtcNow));
        channel.AddExcept(new BanEntry("TestUser!user@host", "op", DateTimeOffset.UtcNow));
        _channels["#test"] = channel;
        _server.GetOrCreateChannel("#test").Returns(channel);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        Assert.True(channel.IsMember("TestUser"));
    }

    [Fact]
    public async Task HandleAsync_ChannelFull_SendsError()
    {
        var channel = new ChannelImpl("#full");
        channel.UserLimit = 1;
        channel.AddMember("Existing", new ChannelMembership { Nickname = "Existing" });
        _channels["#full"] = channel;
        _server.GetOrCreateChannel("#full").Returns(channel);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#full" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_CHANNELISFULL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_CommaChannels_JoinsMultiple()
    {
        var chan1 = new ChannelImpl("#one");
        var chan2 = new ChannelImpl("#two");
        _server.GetOrCreateChannel("#one").Returns(chan1);
        _server.GetOrCreateChannel("#two").Returns(chan2);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#one,#two" });

        await _handler.HandleAsync(session, msg);

        Assert.True(chan1.IsMember("TestUser"));
        Assert.True(chan2.IsMember("TestUser"));
    }

    [Fact]
    public async Task HandleAsync_Join_SendsTopicAndNames()
    {
        var channel = new ChannelImpl("#test");
        channel.Topic = "Hello World";
        channel.TopicSetBy = "admin";
        channel.TopicSetAt = DateTimeOffset.UtcNow;
        _server.GetOrCreateChannel("#test").Returns(channel);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        // Should receive RPL_TOPIC
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_TOPIC,
            Arg.Any<string[]>());
        // Should receive RPL_NAMREPLY
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Any<string[]>());
        // Should receive RPL_ENDOFNAMES
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFNAMES,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_Join_PublishesEvent()
    {
        var channel = new ChannelImpl("#test");
        _server.GetOrCreateChannel("#test").Returns(channel);

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        _events.Received().Publish(Arg.Is<ChannelJoinedEvent>(e =>
            e.Nickname == "TestUser" && e.Channel == "#test"));
    }

    [Fact]
    public async Task HandleAsync_Join_BroadcastsToExistingMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Existing", new ChannelMembership { Nickname = "Existing" });
        _channels["#test"] = channel;
        _server.GetOrCreateChannel("#test").Returns(channel);

        var existingSession = CreateSession("Existing");
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await existingSession.Received().SendMessageAsync(
            Arg.Any<string>(), "JOIN", Arg.Is<string[]>(p => p[0] == "#test"));
    }
}
