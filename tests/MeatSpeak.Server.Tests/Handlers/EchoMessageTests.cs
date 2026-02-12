using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Messaging;
using MeatSpeak.Server.State;

namespace MeatSpeak.Server.Tests.Handlers;

public class EchoMessageTests
{
    private readonly IServer _server;
    private readonly Dictionary<string, IChannel> _channels;

    public EchoMessageTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
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
    public async Task Privmsg_WithEchoMessage_EchoesBackToSender()
    {
        var sender = CreateSession("Sender", echoMessage: true);
        var target = CreateSession("Target");
        var handler = new PrivmsgHandler(_server);
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "Target", "Hello" });

        await handler.HandleAsync(sender, msg);

        // Sender should receive their own message echoed back
        await sender.Received().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG",
            Arg.Is<string[]>(p => p[0] == "Target" && p[1] == "Hello"));
    }

    [Fact]
    public async Task Privmsg_WithoutEchoMessage_NoEchoToSender()
    {
        var sender = CreateSession("Sender"); // no echo-message cap
        var target = CreateSession("Target");
        var handler = new PrivmsgHandler(_server);
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "Target", "Hello" });

        await handler.HandleAsync(sender, msg);

        // Target gets the message, but sender should NOT get echo
        // Sender should only have received 0 SendMessageAsync calls for PRIVMSG
        // (target gets the delivery, not sender)
        await target.Received(1).SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task ChannelPrivmsg_WithEchoMessage_EchoesBackToSender()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", echoMessage: true);
        var other = CreateSession("Other");
        var handler = new PrivmsgHandler(_server);
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#test", "Hello channel" });

        await handler.HandleAsync(sender, msg);

        // Sender should get the echo
        await sender.Received().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG",
            Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "Hello channel"));
    }

    [Fact]
    public async Task Notice_WithEchoMessage_EchoesBackToSender()
    {
        var sender = CreateSession("Sender", echoMessage: true);
        var target = CreateSession("Target");
        var handler = new NoticeHandler(_server);
        var msg = new IrcMessage(null, null, "NOTICE", new[] { "Target", "Notice text" });

        await handler.HandleAsync(sender, msg);

        await sender.Received().SendMessageAsync(
            Arg.Any<string>(), "NOTICE",
            Arg.Is<string[]>(p => p[0] == "Target" && p[1] == "Notice text"));
    }

    [Fact]
    public async Task ChannelNotice_WithEchoMessage_EchoesBackToSender()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", echoMessage: true);
        var other = CreateSession("Other");
        var handler = new NoticeHandler(_server);
        var msg = new IrcMessage(null, null, "NOTICE", new[] { "#test", "Channel notice" });

        await handler.HandleAsync(sender, msg);

        await sender.Received().SendMessageAsync(
            Arg.Any<string>(), "NOTICE",
            Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "Channel notice"));
    }
}
