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
using MeatSpeak.Server.Handlers;
using MeatSpeak.Server.Handlers.Channels;
using MeatSpeak.Server.State;
using Microsoft.Extensions.DependencyInjection;

namespace MeatSpeak.Server.Tests.Handlers;

public class EventPlaybackTests
{
    private readonly IServer _server;
    private readonly DbWriteQueue _writeQueue;
    private readonly IChatLogRepository _repo;
    private readonly ChatHistoryHandler _historyHandler;
    private readonly Dictionary<string, IChannel> _channels;
    private readonly List<(string? tags, string? prefix, string command, string[] parameters)> _sentMessages = new();

    public EventPlaybackTests()
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

        _historyHandler = new ChatHistoryHandler(_server, scopeFactory);
    }

    private void SetupChannel(string name, params string[] members)
    {
        var channel = new ChannelImpl(name);
        foreach (var m in members)
            channel.AddMember(m, new ChannelMembership { Nickname = m });
        _channels[name] = channel;
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

    // --- Event logging tests ---

    [Fact]
    public async Task JoinHandler_LogsJoinEvent()
    {
        var channel = new ChannelImpl("#test");
        _server.GetOrCreateChannel("#test").Returns(channel);

        var handler = new JoinHandler(_server, _writeQueue);
        var session = CreateSession("Alice");
        var info = session.Info;
        info.Channels.Add("placeholder"); info.Channels.Clear(); // ensure real HashSet

        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });
        await handler.HandleAsync(session, msg);

        // Drain the write queue and find the JOIN log
        var items = new List<DbWriteItem>();
        while (_writeQueue.Reader.TryRead(out var item))
            items.Add(item);

        var joinLog = items.OfType<AddChatLog>()
            .FirstOrDefault(l => l.Entity.MessageType == "JOIN");
        Assert.NotNull(joinLog);
        Assert.Equal("#test", joinLog.Entity.ChannelName);
        Assert.Equal("Alice", joinLog.Entity.Sender);
        Assert.NotNull(joinLog.Entity.MsgId);
    }

    [Fact]
    public async Task PartHandler_LogsPartEvent()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice" });
        channel.AddMember("Bob", new ChannelMembership { Nickname = "Bob" });
        _channels["#test"] = channel;

        var handler = new PartHandler(_server, _writeQueue);
        var session = CreateSession("Alice");
        session.Info.Channels.Add("#test");

        var msg = new IrcMessage(null, null, "PART", new[] { "#test", "Leaving" });
        await handler.HandleAsync(session, msg);

        var items = new List<DbWriteItem>();
        while (_writeQueue.Reader.TryRead(out var item))
            items.Add(item);

        var partLog = items.OfType<AddChatLog>()
            .FirstOrDefault(l => l.Entity.MessageType == "PART");
        Assert.NotNull(partLog);
        Assert.Equal("#test", partLog.Entity.ChannelName);
        Assert.Equal("Alice", partLog.Entity.Sender);
        Assert.Equal("Leaving", partLog.Entity.Message);
    }

    [Fact]
    public async Task TopicHandler_LogsTopicEvent()
    {
        var channel = new ChannelImpl("#topic");
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice", IsOperator = true });
        _channels["#topic"] = channel;

        var handler = new TopicHandler(_server, _writeQueue);
        var session = CreateSession("Alice");

        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#topic", "New topic!" });
        await handler.HandleAsync(session, msg);

        // Verify topic was actually set (handler reached that point)
        Assert.Equal("New topic!", channel.Topic);

        var items = new List<DbWriteItem>();
        while (_writeQueue.Reader.TryRead(out var item))
            items.Add(item);

        var topicLog = items.OfType<AddChatLog>()
            .FirstOrDefault(l => l.Entity.MessageType == "TOPIC");
        Assert.NotNull(topicLog);
        Assert.Equal("#topic", topicLog.Entity.ChannelName);
        Assert.Equal("Alice", topicLog.Entity.Sender);
        Assert.Equal("New topic!", topicLog.Entity.Message);
    }

    [Fact]
    public async Task KickHandler_LogsKickEvent()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("OpUser", new ChannelMembership { Nickname = "OpUser", IsOperator = true });
        channel.AddMember("BadUser", new ChannelMembership { Nickname = "BadUser" });
        _channels["#test"] = channel;

        var handler = new KickHandler(_server, _writeQueue);
        var opSession = CreateSession("OpUser");
        var badSession = CreateSession("BadUser");
        badSession.Info.Channels.Add("#test");

        var msg = new IrcMessage(null, null, "KICK", new[] { "#test", "BadUser", "Bye!" });
        await handler.HandleAsync(opSession, msg);

        var items = new List<DbWriteItem>();
        while (_writeQueue.Reader.TryRead(out var item))
            items.Add(item);

        var kickLog = items.OfType<AddChatLog>()
            .FirstOrDefault(l => l.Entity.MessageType == "KICK");
        Assert.NotNull(kickLog);
        Assert.Equal("#test", kickLog.Entity.ChannelName);
        Assert.Equal("OpUser", kickLog.Entity.Sender);
        Assert.Equal("BadUser Bye!", kickLog.Entity.Message);
    }

    // --- ChatHistory event-playback replay tests ---

    [Fact]
    public async Task ChatHistory_WithEventPlayback_ReplaysEvents()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("TestUser", "draft/chathistory", "draft/event-playback", "batch", "server-time", "message-tags");
        var now = DateTimeOffset.UtcNow;

        var messages = new List<ChatLogEntity>
        {
            new() { Sender = "Alice", ChannelName = "#test", Message = "", MessageType = "JOIN", MsgId = "j1", SentAt = now.AddMinutes(-5) },
            new() { Sender = "Alice", ChannelName = "#test", Message = "Hello!", MessageType = "PRIVMSG", MsgId = "m1", SentAt = now.AddMinutes(-4) },
            new() { Sender = "Alice", ChannelName = "#test", Message = "Goodbye", MessageType = "PART", MsgId = "p1", SentAt = now.AddMinutes(-3) },
        };

        _repo.GetLatestAsync("#test", "TestUser", 10, Arg.Any<CancellationToken>()).Returns(messages);

        var msg = new IrcMessage(null, null, "CHATHISTORY", new[] { "LATEST", "#test", "*", "10" });
        await _historyHandler.HandleAsync(session, msg);

        // Find the commands sent within the batch
        var batchMessages = _sentMessages
            .Where(m => m.command != "BATCH")
            .ToList();

        Assert.Equal(3, batchMessages.Count);
        Assert.Equal("JOIN", batchMessages[0].command);
        Assert.Equal("#test", batchMessages[0].parameters[0]);
        Assert.Equal("PRIVMSG", batchMessages[1].command);
        Assert.Equal("PART", batchMessages[2].command);
        Assert.Equal("#test", batchMessages[2].parameters[0]);
        Assert.Equal("Goodbye", batchMessages[2].parameters[1]);
    }

    [Fact]
    public async Task ChatHistory_WithoutEventPlayback_FiltersEvents()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("TestUser", "draft/chathistory", "batch", "server-time", "message-tags");
        var now = DateTimeOffset.UtcNow;

        var messages = new List<ChatLogEntity>
        {
            new() { Sender = "Alice", ChannelName = "#test", Message = "", MessageType = "JOIN", MsgId = "j1", SentAt = now.AddMinutes(-5) },
            new() { Sender = "Alice", ChannelName = "#test", Message = "Hello!", MessageType = "PRIVMSG", MsgId = "m1", SentAt = now.AddMinutes(-4) },
            new() { Sender = "Alice", ChannelName = "#test", Message = "Goodbye", MessageType = "PART", MsgId = "p1", SentAt = now.AddMinutes(-3) },
        };

        _repo.GetLatestAsync("#test", "TestUser", 10, Arg.Any<CancellationToken>()).Returns(messages);

        var msg = new IrcMessage(null, null, "CHATHISTORY", new[] { "LATEST", "#test", "*", "10" });
        await _historyHandler.HandleAsync(session, msg);

        // Should only see PRIVMSG, not JOIN or PART
        var batchMessages = _sentMessages
            .Where(m => m.command != "BATCH")
            .ToList();

        Assert.Single(batchMessages);
        Assert.Equal("PRIVMSG", batchMessages[0].command);
    }

    [Fact]
    public async Task ChatHistory_EventPlayback_ReplaysQuitWithReason()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("TestUser", "draft/chathistory", "draft/event-playback", "batch", "server-time", "message-tags");
        var now = DateTimeOffset.UtcNow;

        var messages = new List<ChatLogEntity>
        {
            new() { Sender = "Bob", ChannelName = "#test", Message = "Connection closed", MessageType = "QUIT", MsgId = "q1", SentAt = now.AddMinutes(-2) },
        };

        _repo.GetLatestAsync("#test", "TestUser", 10, Arg.Any<CancellationToken>()).Returns(messages);

        var msg = new IrcMessage(null, null, "CHATHISTORY", new[] { "LATEST", "#test", "*", "10" });
        await _historyHandler.HandleAsync(session, msg);

        var quitMessages = _sentMessages
            .Where(m => m.command == "QUIT")
            .ToList();

        Assert.Single(quitMessages);
        Assert.Equal("Connection closed", quitMessages[0].parameters[0]);
    }

    [Fact]
    public async Task ChatHistory_EventPlayback_ReplaysTopicChange()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("TestUser", "draft/chathistory", "draft/event-playback", "batch", "server-time", "message-tags");
        var now = DateTimeOffset.UtcNow;

        var messages = new List<ChatLogEntity>
        {
            new() { Sender = "Alice", ChannelName = "#test", Message = "Welcome to #test", MessageType = "TOPIC", MsgId = "t1", SentAt = now.AddMinutes(-1) },
        };

        _repo.GetLatestAsync("#test", "TestUser", 10, Arg.Any<CancellationToken>()).Returns(messages);

        var msg = new IrcMessage(null, null, "CHATHISTORY", new[] { "LATEST", "#test", "*", "10" });
        await _historyHandler.HandleAsync(session, msg);

        var topicMessages = _sentMessages
            .Where(m => m.command == "TOPIC")
            .ToList();

        Assert.Single(topicMessages);
        Assert.Equal("#test", topicMessages[0].parameters[0]);
        Assert.Equal("Welcome to #test", topicMessages[0].parameters[1]);
    }

    [Fact]
    public async Task ChatHistory_EventPlayback_ReplaysKick()
    {
        SetupChannel("#test", "TestUser");
        var session = CreateSession("TestUser", "draft/chathistory", "draft/event-playback", "batch", "server-time", "message-tags");
        var now = DateTimeOffset.UtcNow;

        var messages = new List<ChatLogEntity>
        {
            new() { Sender = "OpUser", ChannelName = "#test", Message = "BadUser Spam", MessageType = "KICK", MsgId = "k1", SentAt = now.AddMinutes(-1) },
        };

        _repo.GetLatestAsync("#test", "TestUser", 10, Arg.Any<CancellationToken>()).Returns(messages);

        var msg = new IrcMessage(null, null, "CHATHISTORY", new[] { "LATEST", "#test", "*", "10" });
        await _historyHandler.HandleAsync(session, msg);

        var kickMessages = _sentMessages
            .Where(m => m.command == "KICK")
            .ToList();

        Assert.Single(kickMessages);
        Assert.Equal("#test", kickMessages[0].parameters[0]);
        Assert.Equal("BadUser", kickMessages[0].parameters[1]);
        Assert.Equal("Spam", kickMessages[0].parameters[2]);
    }
}
