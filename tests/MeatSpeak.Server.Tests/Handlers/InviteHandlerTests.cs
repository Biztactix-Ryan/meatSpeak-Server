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

public class InviteHandlerTests
{
    private readonly IServer _server;
    private readonly InviteHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public InviteHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new InviteHandler(_server);
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
        var msg = new IrcMessage(null, null, "INVITE", new[] { "Target" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_TargetNotFound_SendsError()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "INVITE", new[] { "NonExistent", "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHNICK,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_TargetAlreadyOnChannel_SendsError()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Target", new ChannelMembership { Nickname = "Target" });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var targetSession = CreateSession("Target");
        var msg = new IrcMessage(null, null, "INVITE", new[] { "Target", "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_USERONCHANNEL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_InviteOnlyNotOp_SendsError()
    {
        var channel = new ChannelImpl("#test");
        channel.Modes.Add('i');
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = false });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var targetSession = CreateSession("Target");
        var msg = new IrcMessage(null, null, "INVITE", new[] { "Target", "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_CHANOPRIVSNEEDED,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ValidInvite_SendsInvitingAndInvite()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var targetSession = CreateSession("Target");
        var msg = new IrcMessage(null, null, "INVITE", new[] { "Target", "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_INVITING,
            Arg.Any<string[]>());
        await targetSession.Received().SendMessageAsync(
            Arg.Any<string>(), "INVITE", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_InviteOnlyWithOp_AddsToInviteList()
    {
        var channel = new ChannelImpl("#test");
        channel.Modes.Add('i');
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var targetSession = CreateSession("Target");
        var msg = new IrcMessage(null, null, "INVITE", new[] { "Target", "#test" });

        await _handler.HandleAsync(session, msg);

        Assert.True(channel.IsInvited("Target"));
    }

    [Fact]
    public async Task HandleAsync_ChannelDoesNotExist_StillSendsInvite()
    {
        var session = CreateSession("TestUser");
        var targetSession = CreateSession("Target");
        var msg = new IrcMessage(null, null, "INVITE", new[] { "Target", "#newchan" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_INVITING,
            Arg.Any<string[]>());
        await targetSession.Received().SendMessageAsync(
            Arg.Any<string>(), "INVITE", Arg.Any<string[]>());
    }
}
