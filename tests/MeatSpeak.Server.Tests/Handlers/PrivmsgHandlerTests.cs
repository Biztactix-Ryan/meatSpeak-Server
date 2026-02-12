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
}
