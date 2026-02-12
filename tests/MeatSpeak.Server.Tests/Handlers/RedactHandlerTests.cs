using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;
using MeatSpeak.Server.Handlers.Messaging;
using MeatSpeak.Server.State;
using Microsoft.Extensions.DependencyInjection;

namespace MeatSpeak.Server.Tests.Handlers;

public class RedactHandlerTests
{
    private readonly IServer _server;
    private readonly DbWriteQueue _writeQueue;
    private readonly IChatLogRepository _repo;
    private readonly RedactHandler _handler;
    private readonly RedactHandler _handlerNoDb;
    private readonly Dictionary<string, IChannel> _channels;

    public RedactHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _writeQueue = new DbWriteQueue();

        _repo = Substitute.For<IChatLogRepository>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IChatLogRepository)).Returns(_repo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        _handler = new RedactHandler(_server, _writeQueue, scopeFactory);
        _handlerNoDb = new RedactHandler(_server, _writeQueue);
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
    public async Task HandleAsync_WithoutCap_SendsFail()
    {
        var sender = CreateSession("Sender");
        var msg = new IrcMessage(null, null, "REDACT", new[] { "#test", "msgid123" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "REDACT" && p[1] == "NEED_CAPABILITY"));
    }

    [Fact]
    public async Task HandleAsync_TooFewParams_SendsError()
    {
        var sender = CreateSession("Sender", "draft/message-redaction");
        var msg = new IrcMessage(null, null, "REDACT", new[] { "#test" });

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_OwnMessage_AllowedInChannel()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("User1", new ChannelMembership { Nickname = "User1" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "draft/message-redaction", "server-time");
        var user1 = CreateSession("User1", "draft/message-redaction", "server-time");

        _repo.GetByMsgIdAsync("msgid123", Arg.Any<CancellationToken>())
            .Returns(new ChatLogEntity { MsgId = "msgid123", Sender = "Sender", ChannelName = "#test" });

        var msg = new IrcMessage(null, null, "REDACT", new[] { "#test", "msgid123" });
        await _handler.HandleAsync(sender, msg);

        // Should succeed - forwarded to cap members
        await user1.Received().SendTaggedMessageAsync(
            Arg.Any<string?>(), Arg.Any<string>(), "REDACT",
            Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "msgid123"));

        // Should queue to DB
        Assert.True(_writeQueue.Reader.TryRead(out var item));
        var redact = Assert.IsType<RedactChatLog>(item);
        Assert.Equal("msgid123", redact.MsgId);
    }

    [Fact]
    public async Task HandleAsync_OtherUserMessage_ForbiddenForNonOp()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "draft/message-redaction");

        _repo.GetByMsgIdAsync("msgid123", Arg.Any<CancellationToken>())
            .Returns(new ChatLogEntity { MsgId = "msgid123", Sender = "Alice", ChannelName = "#test" });

        var msg = new IrcMessage(null, null, "REDACT", new[] { "#test", "msgid123" });
        await _handler.HandleAsync(sender, msg);

        // Should get REDACT_FORBIDDEN
        await sender.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "REDACT" && p[1] == "REDACT_FORBIDDEN"));
    }

    [Fact]
    public async Task HandleAsync_OtherUserMessage_AllowedForChannelOp()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("OpUser", new ChannelMembership { Nickname = "OpUser", IsOperator = true });
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice" });
        _channels["#test"] = channel;

        var opUser = CreateSession("OpUser", "draft/message-redaction", "server-time");
        var alice = CreateSession("Alice", "draft/message-redaction", "server-time");

        _repo.GetByMsgIdAsync("msgid123", Arg.Any<CancellationToken>())
            .Returns(new ChatLogEntity { MsgId = "msgid123", Sender = "Alice", ChannelName = "#test" });

        var msg = new IrcMessage(null, null, "REDACT", new[] { "#test", "msgid123" });
        await _handler.HandleAsync(opUser, msg);

        // Should succeed - op can redact others' messages
        await alice.Received().SendTaggedMessageAsync(
            Arg.Any<string?>(), Arg.Any<string>(), "REDACT",
            Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "msgid123"));
    }

    [Fact]
    public async Task HandleAsync_UnknownMsgId_SendsFail()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "draft/message-redaction");

        _repo.GetByMsgIdAsync("nonexistent", Arg.Any<CancellationToken>())
            .Returns((ChatLogEntity?)null);

        var msg = new IrcMessage(null, null, "REDACT", new[] { "#test", "nonexistent" });
        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "REDACT" && p[1] == "UNKNOWN_MSGID"));
    }

    [Fact]
    public async Task HandleAsync_PrivateRedact_OnlySenderCanRedact()
    {
        var sender = CreateSession("Sender", "draft/message-redaction", "server-time");
        var target = CreateSession("Target", "draft/message-redaction", "server-time");

        _repo.GetByMsgIdAsync("msgid123", Arg.Any<CancellationToken>())
            .Returns(new ChatLogEntity { MsgId = "msgid123", Sender = "Sender", Target = "Target" });

        var msg = new IrcMessage(null, null, "REDACT", new[] { "Target", "msgid123" });
        await _handler.HandleAsync(sender, msg);

        // Sender redacting own PM - should succeed
        await target.Received().SendTaggedMessageAsync(
            Arg.Any<string?>(), Arg.Any<string>(), "REDACT",
            Arg.Is<string[]>(p => p[0] == "Target" && p[1] == "msgid123"));
    }

    [Fact]
    public async Task HandleAsync_PrivateRedact_OtherUserForbidden()
    {
        var sender = CreateSession("Sender", "draft/message-redaction");
        CreateSession("Target", "draft/message-redaction");

        _repo.GetByMsgIdAsync("msgid123", Arg.Any<CancellationToken>())
            .Returns(new ChatLogEntity { MsgId = "msgid123", Sender = "Target", Target = "Sender" });

        var msg = new IrcMessage(null, null, "REDACT", new[] { "Target", "msgid123" });
        await _handler.HandleAsync(sender, msg);

        // Sender trying to redact Target's message - should be forbidden
        await sender.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "REDACT" && p[1] == "REDACT_FORBIDDEN"));
    }

    [Fact]
    public async Task HandleAsync_ChannelRedactWithReason_IncludesReason()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("User1", new ChannelMembership { Nickname = "User1" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "draft/message-redaction", "server-time");
        var user1 = CreateSession("User1", "draft/message-redaction", "server-time");

        _repo.GetByMsgIdAsync("msgid123", Arg.Any<CancellationToken>())
            .Returns(new ChatLogEntity { MsgId = "msgid123", Sender = "Sender", ChannelName = "#test" });

        var msg = new IrcMessage(null, null, "REDACT", new[] { "#test", "msgid123", "spam" });
        await _handler.HandleAsync(sender, msg);

        await user1.Received().SendTaggedMessageAsync(
            Arg.Any<string?>(), Arg.Any<string>(), "REDACT",
            Arg.Is<string[]>(p => p.Length == 3 && p[2] == "spam"));
    }

    [Fact]
    public async Task HandleAsync_QueuesRedactToDb()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "draft/message-redaction");

        _repo.GetByMsgIdAsync("msgid123", Arg.Any<CancellationToken>())
            .Returns(new ChatLogEntity { MsgId = "msgid123", Sender = "Sender", ChannelName = "#test" });

        var msg = new IrcMessage(null, null, "REDACT", new[] { "#test", "msgid123" });
        await _handler.HandleAsync(sender, msg);

        Assert.True(_writeQueue.Reader.TryRead(out var item));
        var redact = Assert.IsType<RedactChatLog>(item);
        Assert.Equal("msgid123", redact.MsgId);
        Assert.Equal("Sender", redact.RedactedBy);
    }

    [Fact]
    public async Task HandleAsync_NoSuchChannel_SendsError()
    {
        var sender = CreateSession("Sender", "draft/message-redaction");
        _repo.GetByMsgIdAsync("msgid123", Arg.Any<CancellationToken>())
            .Returns(new ChatLogEntity { MsgId = "msgid123", Sender = "Sender", ChannelName = "#nonexistent" });

        var msg = new IrcMessage(null, null, "REDACT", new[] { "#nonexistent", "msgid123" });
        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHCHANNEL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NotInChannel_SendsError()
    {
        var channel = new ChannelImpl("#test");
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "draft/message-redaction");
        _repo.GetByMsgIdAsync("msgid123", Arg.Any<CancellationToken>())
            .Returns(new ChatLogEntity { MsgId = "msgid123", Sender = "Sender", ChannelName = "#test" });

        var msg = new IrcMessage(null, null, "REDACT", new[] { "#test", "msgid123" });
        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_CANNOTSENDTOCHAN,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoSuchNick_SendsError()
    {
        var sender = CreateSession("Sender", "draft/message-redaction");
        _repo.GetByMsgIdAsync("msgid123", Arg.Any<CancellationToken>())
            .Returns(new ChatLogEntity { MsgId = "msgid123", Sender = "Sender", Target = "Nobody" });

        var msg = new IrcMessage(null, null, "REDACT", new[] { "Nobody", "msgid123" });
        // Reset the FindSessionByNick for "Nobody" to return null
        _server.FindSessionByNick("Nobody").Returns((ISession?)null);

        await _handler.HandleAsync(sender, msg);

        await sender.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHNICK,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WithoutDb_SkipsPermissionCheck()
    {
        // Handler without scopeFactory skips permission validation
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("User1", new ChannelMembership { Nickname = "User1" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender", "draft/message-redaction", "server-time");
        var user1 = CreateSession("User1", "draft/message-redaction", "server-time");

        var msg = new IrcMessage(null, null, "REDACT", new[] { "#test", "msgid123" });
        await _handlerNoDb.HandleAsync(sender, msg);

        // Should still forward without DB check
        await user1.Received().SendTaggedMessageAsync(
            Arg.Any<string?>(), Arg.Any<string>(), "REDACT",
            Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "msgid123"));
    }
}
