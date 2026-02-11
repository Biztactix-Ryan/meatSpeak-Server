using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Handlers.Channels;
using MeatSpeak.Server.State;

namespace MeatSpeak.Server.Tests.Handlers;

public class ModeHandlerTests
{
    private readonly IServer _server;
    private readonly IEventBus _events;
    private readonly ModeHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;
    private readonly ModeRegistry _modes;

    public ModeHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _events = Substitute.For<IEventBus>();
        _server.Events.Returns(_events);
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _modes = new ModeRegistry();
        _modes.RegisterStandardModes();
        _server.Modes.Returns(_modes);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new ModeHandler(_server);
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
        var msg = new IrcMessage(null, null, "MODE", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    // --- User mode tests ---

    [Fact]
    public async Task HandleAsync_UserModeQuery_SendsCurrentModes()
    {
        var session = CreateSession("TestUser");
        session.Info.UserModes.Add('i');
        var msg = new IrcMessage(null, null, "MODE", new[] { "TestUser" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "MODE",
            Arg.Is<string[]>(p => p[0] == "TestUser" && p[1] == "+i"));
    }

    [Fact]
    public async Task HandleAsync_UserModeSetInvisible_SetsMode()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "MODE", new[] { "TestUser", "+i" });

        await _handler.HandleAsync(session, msg);

        Assert.Contains('i', session.Info.UserModes);
    }

    [Fact]
    public async Task HandleAsync_UserModeRemoveOper_RemovesMode()
    {
        var session = CreateSession("TestUser");
        session.Info.UserModes.Add('o');
        var msg = new IrcMessage(null, null, "MODE", new[] { "TestUser", "-o" });

        await _handler.HandleAsync(session, msg);

        Assert.DoesNotContain('o', session.Info.UserModes);
    }

    [Fact]
    public async Task HandleAsync_UserModeAddOper_Ignored()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "MODE", new[] { "TestUser", "+o" });

        await _handler.HandleAsync(session, msg);

        Assert.DoesNotContain('o', session.Info.UserModes);
    }

    [Fact]
    public async Task HandleAsync_UserModeOtherUser_SendsError()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "MODE", new[] { "OtherUser" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHNICK,
            Arg.Any<string[]>());
    }

    // --- Channel mode tests ---

    [Fact]
    public async Task HandleAsync_ChannelModeQuery_SendsModeIs()
    {
        var channel = new ChannelImpl("#test"); // defaults: +nt
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_CHANNELMODEIS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChannelModeNoSuchChannel_SendsError()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#nonexistent" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHCHANNEL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChannelModeNotOp_SendsError()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("TestUser", new ChannelMembership { Nickname = "TestUser", IsOperator = false });
        _channels["#test"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+s" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_CHANOPRIVSNEEDED,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChannelModeSetFlag_AddsMode()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+s" });

        await _handler.HandleAsync(session, msg);

        Assert.Contains('s', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeRemoveFlag_RemovesMode()
    {
        var channel = new ChannelImpl("#test"); // has +nt by default
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "-t" });

        await _handler.HandleAsync(session, msg);

        Assert.DoesNotContain('t', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeSetKey_SetsKey()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+k", "secret" });

        await _handler.HandleAsync(session, msg);

        Assert.Equal("secret", channel.Key);
        Assert.Contains('k', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeRemoveKey_RemovesKey()
    {
        var channel = new ChannelImpl("#test");
        channel.Key = "secret";
        channel.Modes.Add('k');
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "-k", "*" });

        await _handler.HandleAsync(session, msg);

        Assert.Null(channel.Key);
        Assert.DoesNotContain('k', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeSetLimit_SetsLimit()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+l", "50" });

        await _handler.HandleAsync(session, msg);

        Assert.Equal(50, channel.UserLimit);
        Assert.Contains('l', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeRemoveLimit_RemovesLimit()
    {
        var channel = new ChannelImpl("#test");
        channel.UserLimit = 50;
        channel.Modes.Add('l');
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "-l" });

        await _handler.HandleAsync(session, msg);

        Assert.Null(channel.UserLimit);
        Assert.DoesNotContain('l', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeOp_GivesOp()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("User", new ChannelMembership { Nickname = "User", IsOperator = false });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+o", "User" });

        await _handler.HandleAsync(session, msg);

        Assert.True(channel.GetMember("User")!.IsOperator);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeDeop_RemovesOp()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("User", new ChannelMembership { Nickname = "User", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "-o", "User" });

        await _handler.HandleAsync(session, msg);

        Assert.False(channel.GetMember("User")!.IsOperator);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeVoice_GivesVoice()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("User", new ChannelMembership { Nickname = "User" });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+v", "User" });

        await _handler.HandleAsync(session, msg);

        Assert.True(channel.GetMember("User")!.HasVoice);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeBan_AddsBan()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+b", "*!*@bad.host" });

        await _handler.HandleAsync(session, msg);

        Assert.Single(channel.Bans);
        Assert.Equal("*!*@bad.host", channel.Bans[0].Mask);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeUnban_RemovesBan()
    {
        var channel = new ChannelImpl("#test");
        channel.AddBan(new BanEntry("*!*@bad.host", "Op", DateTimeOffset.UtcNow));
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "-b", "*!*@bad.host" });

        await _handler.HandleAsync(session, msg);

        Assert.Empty(channel.Bans);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeChange_BroadcastsToMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var opSession = CreateSession("Op");
        var otherSession = CreateSession("Other");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+s" });

        await _handler.HandleAsync(opSession, msg);

        await otherSession.Received().SendMessageAsync(
            Arg.Any<string>(), "MODE", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChannelModeChange_PublishesEvent()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+s" });

        await _handler.HandleAsync(session, msg);

        _events.Received().Publish(Arg.Is<ModeChangedEvent>(e =>
            e.Target == "#test" && e.SetBy == "Op"));
    }

    [Fact]
    public async Task HandleAsync_ChannelModeListBans_ListsBans()
    {
        var channel = new ChannelImpl("#test");
        channel.AddBan(new BanEntry("*!*@bad.host", "Op", DateTimeOffset.UtcNow));
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        // +b with no param lists bans
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+b" });

        await _handler.HandleAsync(session, msg);

        // 367 = ban list entry, 368 = end of ban list
        await session.Received().SendNumericAsync("test.server", 367, Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", 368, Arg.Any<string[]>());
    }
}
