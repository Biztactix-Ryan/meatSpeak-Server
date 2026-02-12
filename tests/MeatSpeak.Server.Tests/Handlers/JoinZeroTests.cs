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

public class JoinZeroTests
{
    private readonly IServer _server;
    private readonly IEventBus _events;
    private readonly JoinHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public JoinZeroTests()
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
    public async Task HandleAsync_JoinZero_PartsAllChannels()
    {
        var channel1 = new ChannelImpl("#chan1");
        var channel2 = new ChannelImpl("#chan2");
        channel1.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        channel2.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        _channels["#chan1"] = channel1;
        _channels["#chan2"] = channel2;

        var session = CreateSession("TestUser");
        session.Info.Channels.Add("#chan1");
        session.Info.Channels.Add("#chan2");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "0" });

        await _handler.HandleAsync(session, msg);

        Assert.Empty(session.Info.Channels);
        Assert.False(channel1.IsMember("TestUser"));
        Assert.False(channel2.IsMember("TestUser"));
    }

    [Fact]
    public async Task HandleAsync_JoinZero_BroadcastsPartToMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var other = CreateSession("Other");
        session.Info.Channels.Add("#test");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "0" });

        await _handler.HandleAsync(session, msg);

        await other.Received().SendMessageAsync(
            Arg.Any<string>(), "PART", Arg.Is<string[]>(p => p[0] == "#test"));
    }

    [Fact]
    public async Task HandleAsync_JoinZero_RemovesEmptyChannels()
    {
        var channel = new ChannelImpl("#solo");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser" });
        _channels["#solo"] = channel;

        var session = CreateSession("TestUser");
        session.Info.Channels.Add("#solo");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "0" });

        await _handler.HandleAsync(session, msg);

        _server.Received().RemoveChannel("#solo");
    }

    [Fact]
    public async Task HandleAsync_JoinZero_NoChannels_DoesNothing()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "JOIN", new[] { "0" });

        await _handler.HandleAsync(session, msg);

        await session.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
    }
}
