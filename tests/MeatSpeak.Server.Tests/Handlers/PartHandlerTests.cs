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

public class PartHandlerTests
{
    private readonly IServer _server;
    private readonly IEventBus _events;
    private readonly PartHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public PartHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _events = Substitute.For<IEventBus>();
        _server.Events.Returns(_events);
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new PartHandler(_server);
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
        var msg = new IrcMessage(null, null, "PART", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoSuchChannel_SendsError()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "PART", new[] { "#nonexistent" });

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
        var msg = new IrcMessage(null, null, "PART", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOTONCHANNEL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ValidPart_RemovesMemberAndBroadcasts()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        session.Info.Channels.Add("#test");
        var otherSession = CreateSession("Other");
        var msg = new IrcMessage(null, null, "PART", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        Assert.False(channel.IsMember("TestUser"));
        Assert.DoesNotContain("#test", session.Info.Channels);
        await otherSession.Received().SendMessageAsync(
            Arg.Any<string>(), "PART", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_PartWithReason_BroadcastsReason()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        session.Info.Channels.Add("#test");
        var msg = new IrcMessage(null, null, "PART", new[] { "#test", "Goodbye" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync(
            Arg.Any<string>(), "PART", Arg.Is<string[]>(p => p.Length == 2 && p[1] == "Goodbye"));
    }

    [Fact]
    public async Task HandleAsync_LastMember_RemovesChannel()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        session.Info.Channels.Add("#test");
        var msg = new IrcMessage(null, null, "PART", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        _server.Received().RemoveChannel("#test");
    }

    [Fact]
    public async Task HandleAsync_Part_PublishesEvent()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        session.Info.Channels.Add("#test");
        var msg = new IrcMessage(null, null, "PART", new[] { "#test", "bye" });

        await _handler.HandleAsync(session, msg);

        _events.Received().Publish(Arg.Is<ChannelPartedEvent>(e =>
            e.Nickname == "TestUser" && e.Channel == "#test" && e.Reason == "bye"));
    }

    [Fact]
    public async Task HandleAsync_CommaChannels_PartsMultiple()
    {
        var chan1 = new ChannelImpl("#one");
        chan1.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        var chan2 = new ChannelImpl("#two");
        chan2.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        _channels["#one"] = chan1;
        _channels["#two"] = chan2;

        var session = CreateSession("TestUser");
        session.Info.Channels.Add("#one");
        session.Info.Channels.Add("#two");
        var msg = new IrcMessage(null, null, "PART", new[] { "#one,#two" });

        await _handler.HandleAsync(session, msg);

        Assert.False(chan1.IsMember("TestUser"));
        Assert.False(chan2.IsMember("TestUser"));
    }
}
