using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;
using MeatSpeak.Server.Handlers;
using MeatSpeak.Server.State;
using Microsoft.Extensions.DependencyInjection;

namespace MeatSpeak.Server.Tests.Handlers;

public class ChatHistoryHandlerTests
{
    private readonly IServer _server;
    private readonly IChatLogRepository _repo;
    private readonly ChatHistoryHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;
    private readonly List<(string? tags, string? prefix, string command, string[] parameters)> _sentMessages = new();

    public ChatHistoryHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });

        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);

        _repo = Substitute.For<IChatLogRepository>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IChatLogRepository)).Returns(_repo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        _handler = new ChatHistoryHandler(_server, scopeFactory);
    }

    private void SetupChannel(string name, params string[] members)
    {
        var channel = new ChannelImpl(name);
        foreach (var m in members)
            channel.AddMember(m, new ChannelMembership { Nickname = m });
        _channels[name] = channel;
    }

    private ISession CreateSession(params string[] caps)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = "TestUser", Username = "user", Hostname = "host" };
        foreach (var cap in caps)
            info.CapState.Acknowledged.Add(cap);
        session.Info.Returns(info);
        session.Id.Returns("test-id");

        session.SendTaggedMessageAsync(Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string[]>())
            .Returns(ci =>
            {
                _sentMessages.Add((ci.ArgAt<string?>(0), ci.ArgAt<string?>(1), ci.ArgAt<string>(2), ci.ArgAt<string[]>(3)));
                return ValueTask.CompletedTask;
            });
        session.SendMessageAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<string[]>())
            .Returns(ci =>
            {
                _sentMessages.Add((null, ci.ArgAt<string?>(0), ci.ArgAt<string>(1), ci.ArgAt<string[]>(2)));
                return ValueTask.CompletedTask;
            });

        return session;
    }

    [Fact]
    public async Task HandleAsync_WithoutChathistoryCap_SendsFail()
    {
        var session = CreateSession("message-tags");
        var msg = new IrcMessage(null, null, "CHATHISTORY", new[] { "LATEST", "#test", "*", "10" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "CHATHISTORY" && p[1] == "NEED_CAPABILITY"));
    }

    [Fact]
    public async Task HandleAsync_Latest_ReturnsMessagesInBatch()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("draft/chathistory", "batch", "server-time", "message-tags");
        var sentAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var messages = new List<ChatLogEntity>
        {
            new() { Sender = "Alice", ChannelName = "#test", Message = "Hello", MessageType = "PRIVMSG", MsgId = "msg1", SentAt = sentAt },
            new() { Sender = "Bob", ChannelName = "#test", Message = "Hi", MessageType = "PRIVMSG", MsgId = "msg2", SentAt = sentAt.AddMinutes(1) },
        };

        _repo.GetLatestAsync("#test", "TestUser", 10, Arg.Any<CancellationToken>()).Returns(messages);

        var msg = new IrcMessage(null, null, "CHATHISTORY", new[] { "LATEST", "#test", "*", "10" });
        await _handler.HandleAsync(session, msg);

        await session.Received().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Is<string?>(p => p == null), "BATCH",
            Arg.Is<string[]>(p => p[0].StartsWith("+") && p[1] == "chathistory"));
    }

    [Fact]
    public async Task HandleAsync_ChannelNotMember_SendsInvalidTarget()
    {
        SetupChannel("#test", "OtherUser"); // TestUser is NOT a member
        var session = CreateSession("draft/chathistory", "batch", "server-time");

        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "LATEST", "#test", "*", "10" });
        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "CHATHISTORY" && p[1] == "INVALID_TARGET"));
    }

    [Fact]
    public async Task HandleAsync_ChannelDoesNotExist_SendsInvalidTarget()
    {
        // No channel set up
        var session = CreateSession("draft/chathistory", "batch", "server-time");

        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "LATEST", "#nonexistent", "*", "10" });
        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "CHATHISTORY" && p[1] == "INVALID_TARGET"));
    }

    [Fact]
    public async Task HandleAsync_PmTarget_AllowedWithoutChannelCheck()
    {
        var session = CreateSession("draft/chathistory", "batch", "server-time");
        _repo.GetLatestAsync("Bob", "TestUser", 10, Arg.Any<CancellationToken>()).Returns(new List<ChatLogEntity>());

        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "LATEST", "Bob", "*", "10" });
        await _handler.HandleAsync(session, msg);

        await _repo.Received().GetLatestAsync("Bob", "TestUser", 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Before_QueriesWithTimestamp()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("draft/chathistory", "batch", "server-time");
        var ts = DateTimeOffset.UtcNow.AddHours(-1);
        _repo.GetBeforeAsync("#test", "TestUser", Arg.Any<DateTimeOffset>(), 5, Arg.Any<CancellationToken>())
            .Returns(new List<ChatLogEntity>());

        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "BEFORE", "#test", $"timestamp={ts:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}", "5" });
        await _handler.HandleAsync(session, msg);

        await _repo.Received().GetBeforeAsync("#test", "TestUser", Arg.Any<DateTimeOffset>(), 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_After_QueriesWithTimestamp()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("draft/chathistory", "batch", "server-time");
        _repo.GetAfterAsync("#test", "TestUser", Arg.Any<DateTimeOffset>(), 10, Arg.Any<CancellationToken>())
            .Returns(new List<ChatLogEntity>());

        var ts = DateTimeOffset.UtcNow.AddHours(-2);
        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "AFTER", "#test", $"timestamp={ts:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}", "10" });
        await _handler.HandleAsync(session, msg);

        await _repo.Received().GetAfterAsync("#test", "TestUser", Arg.Any<DateTimeOffset>(), 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Around_QueriesWithTimestamp()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("draft/chathistory", "batch", "server-time");
        _repo.GetAroundAsync("#test", "TestUser", Arg.Any<DateTimeOffset>(), 10, Arg.Any<CancellationToken>())
            .Returns(new List<ChatLogEntity>());

        var ts = DateTimeOffset.UtcNow.AddMinutes(-30);
        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "AROUND", "#test", $"timestamp={ts:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}", "10" });
        await _handler.HandleAsync(session, msg);

        await _repo.Received().GetAroundAsync("#test", "TestUser", Arg.Any<DateTimeOffset>(), 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Between_QueriesWithTimestamps()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("draft/chathistory", "batch", "server-time");
        _repo.GetBetweenAsync("#test", "TestUser", Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), 20, Arg.Any<CancellationToken>())
            .Returns(new List<ChatLogEntity>());

        var from = DateTimeOffset.UtcNow.AddHours(-2);
        var to = DateTimeOffset.UtcNow;
        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "BETWEEN", "#test", $"timestamp={from:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}", $"timestamp={to:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}", "20" });
        await _handler.HandleAsync(session, msg);

        await _repo.Received().GetBetweenAsync("#test", "TestUser", Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), 20, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Before_WithMsgIdReference()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("draft/chathistory", "batch", "server-time");
        var refMsg = new ChatLogEntity { MsgId = "ref123", SentAt = DateTimeOffset.UtcNow.AddMinutes(-10) };
        _repo.GetByMsgIdAsync("ref123", Arg.Any<CancellationToken>()).Returns(refMsg);
        _repo.GetBeforeAsync("#test", "TestUser", Arg.Any<DateTimeOffset>(), 5, Arg.Any<CancellationToken>())
            .Returns(new List<ChatLogEntity>());

        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "BEFORE", "#test", "msgid=ref123", "5" });
        await _handler.HandleAsync(session, msg);

        await _repo.Received().GetByMsgIdAsync("ref123", Arg.Any<CancellationToken>());
        await _repo.Received().GetBeforeAsync("#test", "TestUser", Arg.Any<DateTimeOffset>(), 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Targets_ReturnsDistinctTargets()
    {
        SetupChannel("#general", "TestUser");
        SetupChannel("#help", "TestUser");
        var session = CreateSession("draft/chathistory", "batch", "server-time");
        var now = DateTimeOffset.UtcNow;
        _repo.GetTargetsAsync("TestUser", Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), 10, Arg.Any<CancellationToken>())
            .Returns(new List<(string Target, DateTimeOffset LatestMessageAt)> { ("#general", now.AddMinutes(-5)), ("#help", now.AddMinutes(-10)) });

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow;
        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "TARGETS", $"timestamp={from:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}", $"timestamp={to:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}", "10" });
        await _handler.HandleAsync(session, msg);

        await _repo.Received().GetTargetsAsync("TestUser", Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), 10, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Targets_FiltersChannelsNotMember()
    {
        SetupChannel("#general", "TestUser");
        SetupChannel("#secret", "OtherUser"); // TestUser is NOT a member
        var session = CreateSession("draft/chathistory", "batch", "server-time");
        var now = DateTimeOffset.UtcNow;
        _repo.GetTargetsAsync("TestUser", Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), 10, Arg.Any<CancellationToken>())
            .Returns(new List<(string Target, DateTimeOffset LatestMessageAt)> { ("#general", now.AddMinutes(-5)), ("#secret", now.AddMinutes(-10)) });

        var from = DateTimeOffset.UtcNow.AddDays(-1);
        var to = DateTimeOffset.UtcNow;
        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "TARGETS", $"timestamp={from:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}", $"timestamp={to:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}", "10" });
        await _handler.HandleAsync(session, msg);

        // Should only see #general in the response, not #secret
        var chathistoryMessages = _sentMessages
            .Where(m => m.command == "CHATHISTORY" && m.parameters.Length >= 2 && m.parameters[0] == "TARGETS")
            .ToList();
        Assert.Single(chathistoryMessages);
        Assert.Equal("#general", chathistoryMessages[0].parameters[1]);
    }

    [Fact]
    public async Task HandleAsync_UnknownSubcommand_SendsFail()
    {
        var session = CreateSession("draft/chathistory", "batch");
        var msg = new IrcMessage(null, null, "CHATHISTORY", new[] { "INVALID" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "CHATHISTORY" && p[1] == "UNKNOWN_COMMAND"));
    }

    [Fact]
    public async Task HandleAsync_InvalidLimit_SendsFail()
    {
        var session = CreateSession("draft/chathistory", "batch");
        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "LATEST", "#test", "*", "notanumber" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "CHATHISTORY" && p[1] == "INVALID_PARAMS"));
    }

    [Fact]
    public async Task HandleAsync_TooFewParams_SendsFail()
    {
        var session = CreateSession("draft/chathistory", "batch");
        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "LATEST", "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "CHATHISTORY" && p[1] == "NEED_MORE_PARAMS"));
    }

    [Fact]
    public async Task HandleAsync_LimitCappedAtMax()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("draft/chathistory", "batch", "server-time");
        _repo.GetLatestAsync("#test", "TestUser", 100, Arg.Any<CancellationToken>()).Returns(new List<ChatLogEntity>());

        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "LATEST", "#test", "*", "500" });
        await _handler.HandleAsync(session, msg);

        await _repo.Received().GetLatestAsync("#test", "TestUser", 100, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_InvalidReference_SendsFail()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("draft/chathistory", "batch");
        _repo.GetByMsgIdAsync("nonexistent", Arg.Any<CancellationToken>()).Returns((ChatLogEntity?)null);

        var msg = new IrcMessage(null, null, "CHATHISTORY",
            new[] { "BEFORE", "#test", "msgid=nonexistent", "10" });
        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "FAIL",
            Arg.Is<string[]>(p => p[0] == "CHATHISTORY" && p[1] == "INVALID_PARAMS"));
    }
}
