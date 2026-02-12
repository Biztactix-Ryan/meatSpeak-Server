using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Messaging;
using MeatSpeak.Server.State;

namespace MeatSpeak.Server.Tests.Handlers;

public class PrivmsgHandlerTests
{
    private readonly IServer _server;
    private readonly IEventBus _events;
    private readonly PrivmsgHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public PrivmsgHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _events = Substitute.For<IEventBus>();
        _server.Events.Returns(_events);
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new PrivmsgHandler(_server);
    }

    private ISession CreateSession(string nick, bool echoMessage = false)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        if (echoMessage)
            info.CapState.Acknowledged.Add("echo-message");
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task HandleAsync_NoParams_SendsNoRecipient()
    {
        var session = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "PRIVMSG", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NORECIPIENT,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoText_SendsNoTextToSend()
    {
        var session = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "Target" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOTEXTTOSEND,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_PrivateMessage_DeliversToTarget()
    {
        var sender = CreateSession("Sender");
        var target = CreateSession("Target");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "Target", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        await target.Received().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG", Arg.Is<string[]>(p => p[0] == "Target" && p[1] == "Hello!"));
    }

    [Fact]
    public async Task HandleAsync_PrivateMessageToAway_SendsRplAway()
    {
        var sender = CreateSession("Sender");
        var target = CreateSession("Target");
        target.Info.AwayMessage = "Gone fishing";
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "Target", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.RPL_AWAY,
            Arg.Is<string[]>(p => p[0] == "Target" && p[1] == "Gone fishing"));
    }

    [Fact]
    public async Task HandleAsync_PrivateMessageToNotAway_NoRplAway()
    {
        var sender = CreateSession("Sender");
        var target = CreateSession("Target");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "Target", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        await sender.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.RPL_AWAY,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoSuchNick_SendsError()
    {
        var sender = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "NonExistent", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHNICK,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChannelMessage_BroadcastsToMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender");
        var other = CreateSession("Other");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#test", "Hello channel!" });

        await _handler.HandleAsync(sender, msg);

        await other.Received().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG", Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "Hello channel!"));
        // Sender should NOT receive their own message
        await sender.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChannelNotMember_SendsCannotSend()
    {
        var channel = new ChannelImpl("#test");
        _channels["#test"] = channel;

        var sender = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#test", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_CANNOTSENDTOCHAN,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoSuchChannel_SendsError()
    {
        var sender = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#nonexistent", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHCHANNEL,
            Arg.Any<string[]>());
    }

    // --- New edge case tests ---

    [Fact(Skip = "PRIVMSG handler does not yet enforce +m (moderated) mode - needs handler update")]
    public async Task HandleAsync_ModeratedChannel_NonVoicedNonOp_SendsCannotSend()
    {
        var channel = new ChannelImpl("#test");
        channel.Modes.Add('m'); // moderated
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#test", "Blocked by +m" });

        await _handler.HandleAsync(sender, msg);

        // Expected: non-voiced, non-op user in +m channel gets ERR_CANNOTSENDTOCHAN
        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_CANNOTSENDTOCHAN,
            Arg.Any<string[]>());
    }

    [Fact(Skip = "PRIVMSG handler does not yet enforce +m (moderated) mode - needs handler update")]
    public async Task HandleAsync_ModeratedChannel_VoicedUser_CanSend()
    {
        var channel = new ChannelImpl("#test");
        channel.Modes.Add('m'); // moderated
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender", HasVoice = true });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender");
        var other = CreateSession("Other");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#test", "Voiced user msg" });

        await _handler.HandleAsync(sender, msg);

        // Voiced user should be able to send in +m channel
        await other.Received().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG",
            Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "Voiced user msg"));
    }

    [Fact(Skip = "PRIVMSG handler does not yet enforce +m (moderated) mode - needs handler update")]
    public async Task HandleAsync_ModeratedChannel_OpUser_CanSend()
    {
        var channel = new ChannelImpl("#test");
        channel.Modes.Add('m'); // moderated
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender", IsOperator = true });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender");
        var other = CreateSession("Other");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#test", "Op user msg" });

        await _handler.HandleAsync(sender, msg);

        // Op should be able to send in +m channel
        await other.Received().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG",
            Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "Op user msg"));
    }

    [Fact]
    public async Task HandleAsync_ChannelMessage_PublishesEvent()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#test", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        _events.Received().Publish(Arg.Is<ChannelMessageEvent>(e =>
            e.Nickname == "Sender" && e.Channel == "#test" && e.Message == "Hello!"));
    }

    [Fact]
    public async Task HandleAsync_PrivateMessage_PublishesEvent()
    {
        var sender = CreateSession("Sender");
        var target = CreateSession("Target");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "Target", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        _events.Received().Publish(Arg.Is<PrivateMessageEvent>(e =>
            e.FromNick == "Sender" && e.ToNick == "Target" && e.Message == "Hello!"));
    }

    [Fact]
    public async Task HandleAsync_ChannelMessage_SkipsSender()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("Other1", new ChannelMembership { Nickname = "Other1" });
        channel.AddMember("Other2", new ChannelMembership { Nickname = "Other2" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender");
        var other1 = CreateSession("Other1");
        var other2 = CreateSession("Other2");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#test", "Broadcast test" });

        await _handler.HandleAsync(sender, msg);

        // Other members receive the message
        await other1.Received(1).SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
        await other2.Received(1).SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
        // Sender does NOT (no echo-message cap)
        await sender.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_EchoMessage_Channel_EchoesBackToSender()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", echoMessage: true);
        var other = CreateSession("Other");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#test", "Echo test" });

        await _handler.HandleAsync(sender, msg);

        // Sender gets the echo back (has echo-message cap)
        await sender.Received().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG",
            Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "Echo test"));
    }

    [Fact]
    public async Task HandleAsync_EchoMessage_Private_EchoesBackToSender()
    {
        var sender = CreateSession("Sender", echoMessage: true);
        var target = CreateSession("Target");
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "Target", "Echo PM" });

        await _handler.HandleAsync(sender, msg);

        // Sender gets the echo back for private message too
        await sender.Received().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG",
            Arg.Is<string[]>(p => p[0] == "Target" && p[1] == "Echo PM"));
    }
}
