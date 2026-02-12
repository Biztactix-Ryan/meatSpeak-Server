using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Handlers.Messaging;
using MeatSpeak.Server.State;

namespace MeatSpeak.Server.Tests.Handlers;

public class TagmsgHandlerTests
{
    private readonly IServer _server;
    private readonly TagmsgHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public TagmsgHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new TagmsgHandler(_server);
    }

    private ISession CreateSession(string nick, params string[] caps)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        foreach (var cap in caps)
            info.CapState.Acknowledged.Add(cap);
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task HandleAsync_WithoutMessageTagsCap_SendsError()
    {
        var sender = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "TAGMSG", new[] { "#test" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_UNKNOWNCOMMAND,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoRecipient_SendsError()
    {
        var sender = CreateSession("Sender", "message-tags");
        var msg = new IrcMessage(null, null, "TAGMSG", Array.Empty<string>());

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NORECIPIENT,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChannelMessage_ForwardsToMessageTagsMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("TagUser", new ChannelMembership { Nickname = "TagUser" });
        channel.AddMember("PlainUser", new ChannelMembership { Nickname = "PlainUser" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "message-tags");
        var tagUser = CreateSession("TagUser", "message-tags", "server-time");
        var plainUser = CreateSession("PlainUser");

        var msg = new IrcMessage("+draft/react=thumbsup", null, "TAGMSG", new[] { "#test" });

        await _handler.HandleAsync(sender, msg);

        // TagUser should receive (has message-tags)
        await tagUser.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.Contains("msgid=") && t.Contains("+draft/react=thumbsup")),
            Arg.Any<string>(), "TAGMSG", Arg.Any<string[]>());

        // PlainUser should NOT receive (no message-tags)
        await plainUser.DidNotReceive().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
        await plainUser.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), "TAGMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_PrivateMessage_ForwardsToTarget()
    {
        var sender = CreateSession("Sender", "message-tags");
        var target = CreateSession("Target", "message-tags", "server-time");

        var msg = new IrcMessage("+draft/react=smile", null, "TAGMSG", new[] { "Target" });

        await _handler.HandleAsync(sender, msg);

        await target.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.Contains("msgid=")),
            Arg.Any<string>(), "TAGMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_PrivateMessage_TargetWithoutCap_NoForward()
    {
        var sender = CreateSession("Sender", "message-tags");
        var target = CreateSession("Target");

        var msg = new IrcMessage(null, null, "TAGMSG", new[] { "Target" });

        await _handler.HandleAsync(sender, msg);

        await target.DidNotReceive().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoSuchNick_SendsError()
    {
        var sender = CreateSession("Sender", "message-tags");
        var msg = new IrcMessage(null, null, "TAGMSG", new[] { "NonExistent" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHNICK,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoSuchChannel_SendsError()
    {
        var sender = CreateSession("Sender", "message-tags");
        var msg = new IrcMessage(null, null, "TAGMSG", new[] { "#nonexistent" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHCHANNEL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NotInChannel_SendsError()
    {
        var channel = new ChannelImpl("#test");
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "message-tags");
        var msg = new IrcMessage(null, null, "TAGMSG", new[] { "#test" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_CANNOTSENDTOCHAN,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_EchoMessage_EchoesBackToSender()
    {
        var sender = CreateSession("Sender", "message-tags", "echo-message");
        var target = CreateSession("Target", "message-tags");

        var msg = new IrcMessage("+draft/react=thumbsup", null, "TAGMSG", new[] { "Target" });

        await _handler.HandleAsync(sender, msg);

        // Sender should receive echo
        await sender.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.Contains("msgid=") && t.Contains("+draft/react=thumbsup")),
            Arg.Any<string>(), "TAGMSG", Arg.Any<string[]>());
    }
}
