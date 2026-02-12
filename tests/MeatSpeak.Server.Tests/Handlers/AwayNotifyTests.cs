using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.State;

namespace MeatSpeak.Server.Tests.Handlers;

public class AwayNotifyTests
{
    private readonly IServer _server;
    private readonly Dictionary<string, IChannel> _channels;
    private readonly AwayHandler _handler;

    public AwayNotifyTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new AwayHandler(_server);
    }

    private ISession CreateSession(string nick, bool awayNotify = false, bool serverTime = false)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        if (awayNotify)
            info.CapState.Acknowledged.Add("away-notify");
        if (serverTime)
            info.CapState.Acknowledged.Add("server-time");
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task SetAway_BroadcastsToChannelMembersWithAwayNotify()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("User", new ChannelMembership { Nickname = "User" });
        channel.AddMember("Watcher", new ChannelMembership { Nickname = "Watcher" });
        _channels["#test"] = channel;

        var user = CreateSession("User");
        user.Info.Channels.Add("#test");
        var watcher = CreateSession("Watcher", awayNotify: true);

        var msg = new IrcMessage(null, null, "AWAY", new[] { "Gone fishing" });
        await _handler.HandleAsync(user, msg);

        await watcher.Received().SendMessageAsync(
            Arg.Any<string>(), "AWAY",
            Arg.Is<string[]>(p => p[0] == "Gone fishing"));
    }

    [Fact]
    public async Task ClearAway_BroadcastsEmptyAwayToMembersWithCap()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("User", new ChannelMembership { Nickname = "User" });
        channel.AddMember("Watcher", new ChannelMembership { Nickname = "Watcher" });
        _channels["#test"] = channel;

        var user = CreateSession("User");
        user.Info.AwayMessage = "Was away";
        user.Info.Channels.Add("#test");
        var watcher = CreateSession("Watcher", awayNotify: true);

        var msg = new IrcMessage(null, null, "AWAY", Array.Empty<string>());
        await _handler.HandleAsync(user, msg);

        // Should receive AWAY with no parameters (un-away)
        await watcher.Received().SendMessageAsync(
            Arg.Any<string>(), "AWAY", Arg.Is<string[]>(p => p.Length == 0));
    }

    [Fact]
    public async Task SetAway_DoesNotBroadcastToMembersWithoutCap()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("User", new ChannelMembership { Nickname = "User" });
        channel.AddMember("NoCap", new ChannelMembership { Nickname = "NoCap" });
        _channels["#test"] = channel;

        var user = CreateSession("User");
        user.Info.Channels.Add("#test");
        var noCap = CreateSession("NoCap"); // no away-notify cap

        var msg = new IrcMessage(null, null, "AWAY", new[] { "Going away" });
        await _handler.HandleAsync(user, msg);

        await noCap.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), "AWAY", Arg.Any<string[]>());
        await noCap.DidNotReceive().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), "AWAY", Arg.Any<string[]>());
    }

    [Fact]
    public async Task SetAway_DeduplicatesAcrossChannels()
    {
        var chan1 = new ChannelImpl("#chan1");
        var chan2 = new ChannelImpl("#chan2");
        chan1.AddMember("User", new ChannelMembership { Nickname = "User" });
        chan1.AddMember("Watcher", new ChannelMembership { Nickname = "Watcher" });
        chan2.AddMember("User", new ChannelMembership { Nickname = "User" });
        chan2.AddMember("Watcher", new ChannelMembership { Nickname = "Watcher" });
        _channels["#chan1"] = chan1;
        _channels["#chan2"] = chan2;

        var user = CreateSession("User");
        user.Info.Channels.Add("#chan1");
        user.Info.Channels.Add("#chan2");
        var watcher = CreateSession("Watcher", awayNotify: true);

        var msg = new IrcMessage(null, null, "AWAY", new[] { "AFK" });
        await _handler.HandleAsync(user, msg);

        // Watcher should only receive the AWAY notification once despite being in 2 shared channels
        await watcher.Received(1).SendMessageAsync(
            Arg.Any<string>(), "AWAY", Arg.Any<string[]>());
    }

    [Fact]
    public async Task SetAway_DoesNotBroadcastToSelf()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("User", new ChannelMembership { Nickname = "User" });
        _channels["#test"] = channel;

        var user = CreateSession("User", awayNotify: true);
        user.Info.Channels.Add("#test");

        var msg = new IrcMessage(null, null, "AWAY", new[] { "Going away" });
        await _handler.HandleAsync(user, msg);

        // User should get RPL_NOWAWAY but NOT the away-notify broadcast
        await user.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), "AWAY", Arg.Any<string[]>());
    }
}
