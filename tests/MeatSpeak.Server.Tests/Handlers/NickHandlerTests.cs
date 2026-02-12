using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Numerics;
using MeatSpeak.Server.State;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeatSpeak.Server.Tests.Handlers;

public class NickHandlerTests
{
    private readonly IServer _server;
    private readonly IEventBus _events;
    private readonly RegistrationPipeline _registration;
    private readonly NickHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public NickHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _events = Substitute.For<IEventBus>();
        _server.Events.Returns(_events);
        _server.TryClaimNick(Arg.Any<string>(), Arg.Any<ISession>()).Returns(true);
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        var numerics = new NumericSender(_server);
        _registration = new RegistrationPipeline(_server, numerics, null, null, NullLogger<RegistrationPipeline>.Instance, new ServerMetrics());
        _handler = new NickHandler(_server, _registration);
    }

    private ISession CreateSession(string nick, SessionState state = SessionState.Registered)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        session.State.Returns(state);
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task HandleAsync_ValidNick_SetsNickname()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        session.State.Returns(SessionState.Connecting);
        var msg = new IrcMessage(null, null, "NICK", new[] { "TestUser" });

        await _handler.HandleAsync(session, msg);

        Assert.Equal("TestUser", info.Nickname);
    }

    [Fact]
    public async Task HandleAsync_NoNick_SendsError()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "NICK", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", 431, Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NickInUse_SendsError()
    {
        _server.TryClaimNick("TakenNick", Arg.Any<ISession>()).Returns(false);

        var session = Substitute.For<ISession>();
        session.Id.Returns("my-session");
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "NICK", new[] { "TakenNick" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", 433, Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_InvalidNick_SendsError()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "NICK", new[] { "123invalid" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", 432, Arg.Any<string[]>());
    }

    // --- New edge case tests ---

    [Fact]
    public async Task HandleAsync_NickStartingWithDash_SendsError()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "NICK", new[] { "-invalidnick" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_ERRONEUSNICKNAME,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NickExactly32Chars_Accepted()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        session.State.Returns(SessionState.Connecting);
        var nick32 = "A" + new string('a', 31); // 32 chars total, starts with letter
        var msg = new IrcMessage(null, null, "NICK", new[] { nick32 });

        await _handler.HandleAsync(session, msg);

        Assert.Equal(nick32, info.Nickname);
        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.ERR_ERRONEUSNICKNAME,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NickExceeds32Chars_SendsError()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var nick33 = "A" + new string('a', 32); // 33 chars total
        var msg = new IrcMessage(null, null, "NICK", new[] { nick33 });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_ERRONEUSNICKNAME,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NickWithSpecialChars_Accepted()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        session.State.Returns(SessionState.Connecting);
        // All allowed special chars: _ - [ ] { } \ ^ `
        var msg = new IrcMessage(null, null, "NICK", new[] { "Nick_-[]{}\\^`" });

        await _handler.HandleAsync(session, msg);

        Assert.Equal("Nick_-[]{}\\^`", info.Nickname);
    }

    [Fact]
    public async Task HandleAsync_NickWithSpace_SendsError()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "NICK", new[] { "nick name" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_ERRONEUSNICKNAME,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NickWithAtSign_SendsError()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "NICK", new[] { "@nick" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_ERRONEUSNICKNAME,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_EmptyNick_SendsError()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "NICK", new[] { "" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_ERRONEUSNICKNAME,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NickChange_BroadcastsToChannelMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("OldNick", new ChannelMembership { Nickname = "OldNick" });
        channel.AddMember("Watcher", new ChannelMembership { Nickname = "Watcher" });
        _channels["#test"] = channel;

        var session = CreateSession("OldNick");
        session.Info.Channels.Add("#test");
        var watcher = CreateSession("Watcher");

        var msg = new IrcMessage(null, null, "NICK", new[] { "NewNick" });

        await _handler.HandleAsync(session, msg);

        // Watcher in shared channel should receive the NICK change
        await watcher.Received().SendMessageAsync(
            Arg.Any<string>(), "NICK", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NickChange_UpdatesChannelMembership()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("OldNick", new ChannelMembership { Nickname = "OldNick", IsOperator = true });
        _channels["#test"] = channel;

        var session = CreateSession("OldNick");
        session.Info.Channels.Add("#test");

        var msg = new IrcMessage(null, null, "NICK", new[] { "NewNick" });

        await _handler.HandleAsync(session, msg);

        // Old membership key should be removed, new one added
        Assert.Null(channel.GetMember("OldNick"));
        Assert.NotNull(channel.GetMember("NewNick"));
        Assert.True(channel.GetMember("NewNick")!.IsOperator);
    }

    [Fact]
    public async Task HandleAsync_NickChange_RecordsWhowas()
    {
        var session = CreateSession("OldNick");

        var msg = new IrcMessage(null, null, "NICK", new[] { "NewNick" });

        await _handler.HandleAsync(session, msg);

        _server.Received().RecordWhowas(Arg.Is<WhowasEntry>(e =>
            e.Nickname == "OldNick" && e.Username == "user" && e.Hostname == "host"));
    }

    [Fact]
    public async Task HandleAsync_NickChange_NotifiesMonitorWatchers()
    {
        var session = CreateSession("OldNick");

        var msg = new IrcMessage(null, null, "NICK", new[] { "NewNick" });

        await _handler.HandleAsync(session, msg);

        // NickChangedEvent should be published
        _events.Received().Publish(Arg.Is<NickChangedEvent>(e =>
            e.OldNick == "OldNick" && e.NewNick == "NewNick"));
    }

    [Fact]
    public async Task HandleAsync_NickChange_DeduplicatesAcrossChannels()
    {
        // Same watcher in two shared channels should only get one NICK broadcast
        var channel1 = new ChannelImpl("#chan1");
        channel1.AddMember("OldNick", new ChannelMembership { Nickname = "OldNick" });
        channel1.AddMember("Watcher", new ChannelMembership { Nickname = "Watcher" });
        _channels["#chan1"] = channel1;

        var channel2 = new ChannelImpl("#chan2");
        channel2.AddMember("OldNick", new ChannelMembership { Nickname = "OldNick" });
        channel2.AddMember("Watcher", new ChannelMembership { Nickname = "Watcher" });
        _channels["#chan2"] = channel2;

        var session = CreateSession("OldNick");
        session.Info.Channels.Add("#chan1");
        session.Info.Channels.Add("#chan2");
        var watcher = CreateSession("Watcher");

        var msg = new IrcMessage(null, null, "NICK", new[] { "NewNick" });

        await _handler.HandleAsync(session, msg);

        // Watcher should only receive 1 NICK message despite being in 2 shared channels
        await watcher.Received(1).SendMessageAsync(
            Arg.Any<string>(), "NICK", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_SameNickDifferentCase_AllowedWithoutClaim()
    {
        // Case-only change: TryClaimNick should not be called because
        // string.Equals with OrdinalIgnoreCase means it's the same nick
        var session = CreateSession("TestUser");

        var msg = new IrcMessage(null, null, "NICK", new[] { "TESTUSER" });

        await _handler.HandleAsync(session, msg);

        // Nick should be updated to new casing
        Assert.Equal("TESTUSER", session.Info.Nickname);
        // TryClaimNick should NOT be called (same nick, different case)
        _server.DidNotReceive().TryClaimNick("TESTUSER", Arg.Any<ISession>());
    }
}
