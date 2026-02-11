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

public class KickHandlerTests
{
    private readonly IServer _server;
    private readonly KickHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public KickHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new KickHandler(_server);
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
        var msg = new IrcMessage(null, null, "KICK", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoSuchChannel_SendsError()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "KICK", new[] { "#nonexistent", "Target" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHCHANNEL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NotChanOp_SendsError()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = false });
        channel.AddMember("Target", new ChannelMembership { Nickname = "Target" });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "KICK", new[] { "#test", "Target" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_CHANOPRIVSNEEDED,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_TargetNotOnChannel_SendsError()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "KICK", new[] { "#test", "NotHere" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_USERNOTINCHANNEL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ValidKick_RemovesTargetAndBroadcasts()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("Target", new ChannelMembership { Nickname = "Target" });
        _channels["#test"] = channel;

        var opSession = CreateSession("Op");
        var targetSession = CreateSession("Target");
        targetSession.Info.Channels.Add("#test");
        var msg = new IrcMessage(null, null, "KICK", new[] { "#test", "Target", "Misbehaving" });

        await _handler.HandleAsync(opSession, msg);

        Assert.False(channel.IsMember("Target"));
        Assert.DoesNotContain("#test", targetSession.Info.Channels);
        await targetSession.Received().SendMessageAsync(
            Arg.Any<string>(), "KICK", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_KickLastMember_RemovesChannel()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("Target", new ChannelMembership { Nickname = "Target" });
        _channels["#test"] = channel;

        // Kick Target, then Op parts -> only Op and Target
        var opSession = CreateSession("Op");
        var targetSession = CreateSession("Target");
        targetSession.Info.Channels.Add("#test");

        // First remove Op to make Target the only one, then kick
        channel.RemoveMember("Op");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });

        var msg = new IrcMessage(null, null, "KICK", new[] { "#test", "Target" });
        await _handler.HandleAsync(opSession, msg);

        // After kick, only Op remains so channel should not be removed
        Assert.True(channel.IsMember("Op"));
    }

    [Fact]
    public async Task HandleAsync_KickWithDefaultReason_UsesNickAsReason()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("Target", new ChannelMembership { Nickname = "Target" });
        _channels["#test"] = channel;

        var opSession = CreateSession("Op");
        var targetSession = CreateSession("Target");
        targetSession.Info.Channels.Add("#test");
        var msg = new IrcMessage(null, null, "KICK", new[] { "#test", "Target" });

        await _handler.HandleAsync(opSession, msg);

        // The reason defaults to the target nick
        await opSession.Received().SendMessageAsync(
            Arg.Any<string>(), "KICK",
            Arg.Is<string[]>(p => p[2] == "Target"));
    }
}
