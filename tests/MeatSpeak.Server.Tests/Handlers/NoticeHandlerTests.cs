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

public class NoticeHandlerTests
{
    private readonly IServer _server;
    private readonly NoticeHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public NoticeHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new NoticeHandler(_server);
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
    public async Task HandleAsync_NoParams_IsNoOp()
    {
        var session = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "NOTICE", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string[]>());
        await session.DidNotReceive().SendMessageAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string[]>());
        await session.DidNotReceive().SendTaggedMessageAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_OneParam_IsNoOp()
    {
        var session = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "NOTICE", new[] { "Target" });

        await _handler.HandleAsync(session, msg);

        await session.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string[]>());
        await session.DidNotReceive().SendMessageAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string[]>());
        await session.DidNotReceive().SendTaggedMessageAsync(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_PrivateMessage_DeliversToTarget()
    {
        var sender = CreateSession("Sender");
        var target = CreateSession("Target");
        var msg = new IrcMessage(null, null, "NOTICE", new[] { "Target", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        await target.Received().SendMessageAsync(
            Arg.Any<string>(), "NOTICE", Arg.Is<string[]>(p => p[0] == "Target" && p[1] == "Hello!"));
    }

    [Fact]
    public async Task HandleAsync_PrivateMessageToUnknownNick_IsSilentlyIgnored()
    {
        var sender = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "NOTICE", new[] { "NonExistent", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        await sender.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChannelMessage_BroadcastsToMembersNotSender()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender");
        var other = CreateSession("Other");
        var msg = new IrcMessage(null, null, "NOTICE", new[] { "#test", "Hello channel!" });

        await _handler.HandleAsync(sender, msg);

        await other.Received().SendMessageAsync(
            Arg.Any<string>(), "NOTICE", Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "Hello channel!"));
        await sender.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), "NOTICE", Arg.Any<string[]>());
        await sender.DidNotReceive().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), "NOTICE", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ChannelNotFound_IsSilentlyIgnored()
    {
        var sender = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "NOTICE", new[] { "#nonexistent", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        await sender.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NotAMember_IsSilentlyIgnored()
    {
        var channel = new ChannelImpl("#test");
        _channels["#test"] = channel;

        var sender = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "NOTICE", new[] { "#test", "Hello!" });

        await _handler.HandleAsync(sender, msg);

        await sender.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(),
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WithEchoMessageCap_EchoesBackToSender()
    {
        var sender = CreateSession("Sender");
        sender.Info.CapState.Acknowledged.Add("echo-message");
        var target = CreateSession("Target");
        var msg = new IrcMessage(null, null, "NOTICE", new[] { "Target", "Notice text" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendMessageAsync(
            Arg.Any<string>(), "NOTICE",
            Arg.Is<string[]>(p => p[0] == "Target" && p[1] == "Notice text"));
    }
}
