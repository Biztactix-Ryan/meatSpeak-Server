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

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_UMODEIS,
            Arg.Is<string[]>(p => p[0] == "+i"));
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

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_USERSDONTMATCH,
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

    // --- Ban exception (+e) tests ---

    [Fact]
    public async Task HandleAsync_ChannelModeAddExcept_AddsExcept()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+e", "*!friend@bad.host" });

        await _handler.HandleAsync(session, msg);

        Assert.Single(channel.Excepts);
        Assert.Equal("*!friend@bad.host", channel.Excepts[0].Mask);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeRemoveExcept_RemovesExcept()
    {
        var channel = new ChannelImpl("#test");
        channel.AddExcept(new BanEntry("*!friend@bad.host", "Op", DateTimeOffset.UtcNow));
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "-e", "*!friend@bad.host" });

        await _handler.HandleAsync(session, msg);

        Assert.Empty(channel.Excepts);
    }

    [Fact]
    public async Task HandleAsync_ChannelModeListExcepts_ListsExcepts()
    {
        var channel = new ChannelImpl("#test");
        channel.AddExcept(new BanEntry("*!friend@bad.host", "Op", DateTimeOffset.UtcNow));
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        // +e with no param lists exceptions
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+e" });

        await _handler.HandleAsync(session, msg);

        // 348 = exception list entry, 349 = end of exception list
        await session.Received().SendNumericAsync("test.server", 348, Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", 349, Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChannelModeAddExcept_BroadcastsToMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var opSession = CreateSession("Op");
        var otherSession = CreateSession("Other");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+e", "*!friend@bad.host" });

        await _handler.HandleAsync(opSession, msg);

        await otherSession.Received().SendMessageAsync(
            Arg.Any<string>(), "MODE", Arg.Any<string[]>());
    }

    // --- New compound/edge case tests ---

    [Fact]
    public async Task HandleAsync_CompoundModes_AppliesMultiple()
    {
        var channel = new ChannelImpl("#test");
        channel.Modes.Remove('n'); // clear defaults for clean test
        channel.Modes.Remove('t');
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+nt" });

        await _handler.HandleAsync(session, msg);

        Assert.Contains('n', channel.Modes);
        Assert.Contains('t', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_MixedDirectionModes_AppliesCorrectly()
    {
        var channel = new ChannelImpl("#test"); // defaults: +nt
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        // +s adds secret, -t removes topic-protected
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+s-t" });

        await _handler.HandleAsync(session, msg);

        Assert.Contains('s', channel.Modes);
        Assert.DoesNotContain('t', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_OpAndVoiceTogether_AppliesBoth()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("User1", new ChannelMembership { Nickname = "User1" });
        channel.AddMember("User2", new ChannelMembership { Nickname = "User2" });
        _channels["#test"] = channel;

        var opSession = CreateSession("Op");
        CreateSession("User1");
        CreateSession("User2");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+ov", "User1", "User2" });

        await _handler.HandleAsync(opSession, msg);

        Assert.True(channel.GetMember("User1")!.IsOperator);
        Assert.True(channel.GetMember("User2")!.HasVoice);
    }

    [Fact]
    public async Task HandleAsync_ModeWithMissingParam_SkipsSilently()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("User", new ChannelMembership { Nickname = "User" });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        // +o with no nick param - should be silently skipped
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+o" });

        await _handler.HandleAsync(session, msg);

        // User should remain non-op
        Assert.False(channel.GetMember("User")!.IsOperator);
        // No event published since no modes were actually applied
        _events.DidNotReceive().Publish(Arg.Any<ModeChangedEvent>());
    }

    [Fact]
    public async Task HandleAsync_LimitNonNumeric_IgnoresMode()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+l", "abc" });

        await _handler.HandleAsync(session, msg);

        Assert.Null(channel.UserLimit);
        Assert.DoesNotContain('l', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_LimitZero_IgnoresMode()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+l", "0" });

        await _handler.HandleAsync(session, msg);

        Assert.Null(channel.UserLimit);
        Assert.DoesNotContain('l', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_LimitNegative_IgnoresMode()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+l", "-5" });

        await _handler.HandleAsync(session, msg);

        Assert.Null(channel.UserLimit);
        Assert.DoesNotContain('l', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_KeyWithoutParam_IgnoresMode()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        // +k with no key param
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+k" });

        await _handler.HandleAsync(session, msg);

        Assert.Null(channel.Key);
        Assert.DoesNotContain('k', channel.Modes);
    }

    [Fact]
    public async Task HandleAsync_UnknownModeChar_IgnoredSilently()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        // 'x' is not registered in ModeRegistry
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+x" });

        await _handler.HandleAsync(session, msg);

        Assert.DoesNotContain('x', channel.Modes);
        // No mode change event since nothing was applied
        _events.DidNotReceive().Publish(Arg.Any<ModeChangedEvent>());
    }

    [Fact]
    public async Task HandleAsync_BanListEmpty_SendsOnlyEndOfList()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        // +b with no param = list bans, but ban list is empty
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+b" });

        await _handler.HandleAsync(session, msg);

        // Should NOT send 367 (ban list entry) since there are no bans
        await session.DidNotReceive().SendNumericAsync("test.server", 367, Arg.Any<string[]>());
        // Should still send 368 (end of ban list)
        await session.Received().SendNumericAsync("test.server", 368, Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ExceptListEmpty_SendsOnlyEndOfList()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        // +e with no param = list exceptions, but list is empty
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+e" });

        await _handler.HandleAsync(session, msg);

        // Should NOT send 348 (exception list entry)
        await session.DidNotReceive().SendNumericAsync("test.server", 348, Arg.Any<string[]>());
        // Should still send 349 (end of exception list)
        await session.Received().SendNumericAsync("test.server", 349, Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_DeopSelf_Allowed()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "-o", "Op" });

        await _handler.HandleAsync(session, msg);

        Assert.False(channel.GetMember("Op")!.IsOperator);
    }

    [Fact]
    public async Task HandleAsync_ModeOnNonMember_SkipsSilently()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        // "GhostNick" is not a member of the channel
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+o", "GhostNick" });

        await _handler.HandleAsync(session, msg);

        // No mode change event since the target wasn't in the channel
        _events.DidNotReceive().Publish(Arg.Any<ModeChangedEvent>());
    }

    [Fact]
    public async Task HandleAsync_ChannelModeQueryShowsKeyToOp()
    {
        var channel = new ChannelImpl("#test");
        channel.Key = "secretkey";
        channel.Modes.Add('k');
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        // Op should see the actual key
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_CHANNELMODEIS,
            Arg.Is<string[]>(p => p.Any(s => s == "secretkey")));
    }

    [Fact]
    public async Task HandleAsync_ChannelModeQueryHidesKeyFromNonOp()
    {
        var channel = new ChannelImpl("#test");
        channel.Key = "secretkey";
        channel.Modes.Add('k');
        channel.AddMember("User", new ChannelMembership { Nickname = "User", IsOperator = false });
        _channels["#test"] = channel;

        var session = CreateSession("User");
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        // Non-op should see "*" instead of the actual key
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_CHANNELMODEIS,
            Arg.Is<string[]>(p => p.Any(s => s == "*") && !p.Any(s => s == "secretkey")));
    }

    [Fact]
    public async Task HandleAsync_RemoveBanWithoutParam_IgnoresSilently()
    {
        var channel = new ChannelImpl("#test");
        channel.AddBan(new BanEntry("*!*@bad.host", "Op", DateTimeOffset.UtcNow));
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("Op");
        // -b with no mask param
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "-b" });

        await _handler.HandleAsync(session, msg);

        // Ban should still be present (not removed)
        Assert.Single(channel.Bans);
        // No mode change event since nothing was applied
        _events.DidNotReceive().Publish(Arg.Any<ModeChangedEvent>());
    }
}
