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

public class ReactReplyTests
{
    private readonly IServer _server;
    private readonly IEventBus _events;
    private readonly PrivmsgHandler _privmsgHandler;
    private readonly TagmsgHandler _tagmsgHandler;
    private readonly Dictionary<string, IChannel> _channels;

    public ReactReplyTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _events = Substitute.For<IEventBus>();
        _server.Events.Returns(_events);
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _privmsgHandler = new PrivmsgHandler(_server);
        _tagmsgHandler = new TagmsgHandler(_server);
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
    public async Task Privmsg_ClientTags_ForwardedToMessageTagsRecipient()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("TagUser", new ChannelMembership { Nickname = "TagUser" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "message-tags");
        var tagUser = CreateSession("TagUser", "message-tags", "server-time");

        // Send PRIVMSG with reply tag
        var msg = new IrcMessage("+draft/reply=origmsg123", null, "PRIVMSG", new[] { "#test", "This is a reply" });
        await _privmsgHandler.HandleAsync(sender, msg);

        await tagUser.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.Contains("+draft/reply=origmsg123") && t.Contains("msgid=")),
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Privmsg_ClientTags_StrippedForNonMessageTagsRecipient()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("PlainUser", new ChannelMembership { Nickname = "PlainUser" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "message-tags");
        var plainUser = CreateSession("PlainUser", "server-time"); // has server-time but not message-tags

        var msg = new IrcMessage("+draft/reply=origmsg123", null, "PRIVMSG", new[] { "#test", "This is a reply" });
        await _privmsgHandler.HandleAsync(sender, msg);

        // PlainUser should get the message but without client tags
        await plainUser.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => !t.Contains("+draft/reply")),
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Tagmsg_ReactTag_ForwardedToChannel()
    {
        var channel = new ChannelImpl("#general");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("Recipient", new ChannelMembership { Nickname = "Recipient" });
        _channels["#general"] = channel;

        var sender = CreateSession("Sender", "message-tags");
        var recipient = CreateSession("Recipient", "message-tags", "server-time");

        var msg = new IrcMessage("+draft/react=thumbsup;+draft/reply=msg123", null, "TAGMSG", new[] { "#general" });
        await _tagmsgHandler.HandleAsync(sender, msg);

        await recipient.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t =>
                t.Contains("+draft/react=thumbsup") &&
                t.Contains("+draft/reply=msg123") &&
                t.Contains("msgid=")),
            Arg.Any<string>(), "TAGMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Tagmsg_ReactTag_NotForwardedToNonMessageTagsUser()
    {
        var channel = new ChannelImpl("#general");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("PlainUser", new ChannelMembership { Nickname = "PlainUser" });
        _channels["#general"] = channel;

        var sender = CreateSession("Sender", "message-tags");
        var plainUser = CreateSession("PlainUser"); // no message-tags cap

        var msg = new IrcMessage("+draft/react=smile", null, "TAGMSG", new[] { "#general" });
        await _tagmsgHandler.HandleAsync(sender, msg);

        // TAGMSG should not be delivered to users without message-tags
        await plainUser.DidNotReceive().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
        await plainUser.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), "TAGMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Privmsg_ReplyAndReact_CombinedTags()
    {
        var sender = CreateSession("Sender", "message-tags");
        var target = CreateSession("Target", "message-tags");

        var msg = new IrcMessage("+draft/reply=ref1;+draft/react=heart", null, "PRIVMSG", new[] { "Target", "Love this!" });
        await _privmsgHandler.HandleAsync(sender, msg);

        await target.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t =>
                t.Contains("+draft/reply=ref1") &&
                t.Contains("+draft/react=heart") &&
                t.Contains("msgid=")),
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Privmsg_EchoMessage_IncludesClientTags()
    {
        var sender = CreateSession("Sender", "message-tags", "echo-message");
        var target = CreateSession("Target", "message-tags");

        var msg = new IrcMessage("+draft/reply=origref", null, "PRIVMSG", new[] { "Target", "Reply text" });
        await _privmsgHandler.HandleAsync(sender, msg);

        // Echo to sender should include client tags
        await sender.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.Contains("+draft/reply=origref") && t.Contains("msgid=")),
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
    }
}
