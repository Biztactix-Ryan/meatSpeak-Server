using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Connection;
using System.Collections.Concurrent;

namespace MeatSpeak.Server.Tests.Handlers;

public class MonitorHandlerTests
{
    private readonly IServer _server;
    private readonly MonitorHandler _handler;
    private readonly ConcurrentDictionary<string, ISession> _sessions = new();

    public MonitorHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _server.Sessions.Returns(_sessions);
        _handler = new MonitorHandler(_server);
    }

    private ISession CreateSession(string nick, SessionState state = SessionState.Registered)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        session.State.Returns(state);
        return session;
    }

    private void RegisterOnServer(ISession session)
    {
        var nick = session.Info.Nickname!;
        _sessions[session.Id] = session;
        _server.FindSessionByNick(nick).Returns(session);
    }

    // --- MONITOR + ---

    [Fact]
    public async Task MonitorAdd_OnlineNick_SendsMononline()
    {
        var watcher = CreateSession("Watcher");
        var target = CreateSession("Alice");
        RegisterOnServer(target);

        var msg = new IrcMessage(null, null, "MONITOR", new[] { "+", "Alice" });
        await _handler.HandleAsync(watcher, msg);

        Assert.Contains("Alice", watcher.Info.MonitorList);
        await watcher.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MONONLINE,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task MonitorAdd_OfflineNick_SendsMonoffline()
    {
        var watcher = CreateSession("Watcher");
        _server.FindSessionByNick("Ghost").Returns((ISession?)null);

        var msg = new IrcMessage(null, null, "MONITOR", new[] { "+", "Ghost" });
        await _handler.HandleAsync(watcher, msg);

        Assert.Contains("Ghost", watcher.Info.MonitorList);
        await watcher.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MONOFFLINE,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task MonitorAdd_MultipleNicks_CommaSeparated()
    {
        var watcher = CreateSession("Watcher");
        var alice = CreateSession("Alice");
        RegisterOnServer(alice);
        _server.FindSessionByNick("Bob").Returns((ISession?)null);

        var msg = new IrcMessage(null, null, "MONITOR", new[] { "+", "Alice,Bob" });
        await _handler.HandleAsync(watcher, msg);

        Assert.Contains("Alice", watcher.Info.MonitorList);
        Assert.Contains("Bob", watcher.Info.MonitorList);
    }

    [Fact]
    public async Task MonitorAdd_ListFull_SendsMonlistfull()
    {
        var watcher = CreateSession("Watcher");
        // Fill up the monitor list
        for (int i = 0; i < 100; i++)
            watcher.Info.MonitorList.Add($"nick{i}");

        var msg = new IrcMessage(null, null, "MONITOR", new[] { "+", "OneMore" });
        await _handler.HandleAsync(watcher, msg);

        Assert.DoesNotContain("OneMore", watcher.Info.MonitorList);
        await watcher.Received().SendNumericAsync("test.server", IrcNumerics.ERR_MONLISTFULL,
            Arg.Any<string[]>());
    }

    // --- MONITOR - ---

    [Fact]
    public async Task MonitorRemove_RemovesNicksFromList()
    {
        var watcher = CreateSession("Watcher");
        watcher.Info.MonitorList.Add("Alice");
        watcher.Info.MonitorList.Add("Bob");

        var msg = new IrcMessage(null, null, "MONITOR", new[] { "-", "Alice" });
        await _handler.HandleAsync(watcher, msg);

        Assert.DoesNotContain("Alice", watcher.Info.MonitorList);
        Assert.Contains("Bob", watcher.Info.MonitorList);
    }

    [Fact]
    public async Task MonitorRemove_MultipleNicks()
    {
        var watcher = CreateSession("Watcher");
        watcher.Info.MonitorList.Add("Alice");
        watcher.Info.MonitorList.Add("Bob");
        watcher.Info.MonitorList.Add("Charlie");

        var msg = new IrcMessage(null, null, "MONITOR", new[] { "-", "Alice,Charlie" });
        await _handler.HandleAsync(watcher, msg);

        Assert.DoesNotContain("Alice", watcher.Info.MonitorList);
        Assert.Contains("Bob", watcher.Info.MonitorList);
        Assert.DoesNotContain("Charlie", watcher.Info.MonitorList);
    }

    // --- MONITOR C ---

    [Fact]
    public async Task MonitorClear_ClearsEntireList()
    {
        var watcher = CreateSession("Watcher");
        watcher.Info.MonitorList.Add("Alice");
        watcher.Info.MonitorList.Add("Bob");

        var msg = new IrcMessage(null, null, "MONITOR", new[] { "C" });
        await _handler.HandleAsync(watcher, msg);

        Assert.Empty(watcher.Info.MonitorList);
    }

    // --- MONITOR L ---

    [Fact]
    public async Task MonitorList_ListsMonitoredNicks()
    {
        var watcher = CreateSession("Watcher");
        watcher.Info.MonitorList.Add("Alice");
        watcher.Info.MonitorList.Add("Bob");

        var msg = new IrcMessage(null, null, "MONITOR", new[] { "L" });
        await _handler.HandleAsync(watcher, msg);

        await watcher.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MONLIST,
            Arg.Any<string[]>());
        await watcher.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFMONLIST,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task MonitorList_EmptyList_SendsEndOnly()
    {
        var watcher = CreateSession("Watcher");

        var msg = new IrcMessage(null, null, "MONITOR", new[] { "L" });
        await _handler.HandleAsync(watcher, msg);

        await watcher.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.RPL_MONLIST,
            Arg.Any<string[]>());
        await watcher.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFMONLIST,
            Arg.Any<string[]>());
    }

    // --- MONITOR S ---

    [Fact]
    public async Task MonitorStatus_ReportsOnlineAndOffline()
    {
        var watcher = CreateSession("Watcher");
        var alice = CreateSession("Alice");
        RegisterOnServer(alice);
        _server.FindSessionByNick("Bob").Returns((ISession?)null);
        watcher.Info.MonitorList.Add("Alice");
        watcher.Info.MonitorList.Add("Bob");

        var msg = new IrcMessage(null, null, "MONITOR", new[] { "S" });
        await _handler.HandleAsync(watcher, msg);

        await watcher.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MONONLINE,
            Arg.Any<string[]>());
        await watcher.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MONOFFLINE,
            Arg.Any<string[]>());
    }

    // --- Missing params ---

    [Fact]
    public async Task Monitor_NoParams_SendsNeedMoreParams()
    {
        var watcher = CreateSession("Watcher");
        var msg = new IrcMessage(null, null, "MONITOR", Array.Empty<string>());

        await _handler.HandleAsync(watcher, msg);

        await watcher.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    // --- Static NotifyOnline / NotifyOffline ---

    [Fact]
    public async Task NotifyOnline_SendsMononlineToWatchers()
    {
        var watcher = CreateSession("Watcher");
        watcher.Info.MonitorList.Add("Alice");
        RegisterOnServer(watcher);

        var alice = CreateSession("Alice");
        RegisterOnServer(alice);

        await MonitorHandler.NotifyOnline(_server, alice);

        // NotifyOnline uses CapHelper.SendWithTimestamp -> SendMessageAsync (no server-time cap)
        await watcher.Received().SendMessageAsync(
            "test.server",
            IrcNumerics.Format(IrcNumerics.RPL_MONONLINE),
            Arg.Is<string[]>(p => p.Length >= 2 && p[1].Contains("Alice")));
    }

    [Fact]
    public async Task NotifyOnline_DoesNotNotifySelf()
    {
        var alice = CreateSession("Alice");
        alice.Info.MonitorList.Add("Alice");
        RegisterOnServer(alice);

        await MonitorHandler.NotifyOnline(_server, alice);

        await alice.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), Arg.Is(IrcNumerics.Format(IrcNumerics.RPL_MONONLINE)), Arg.Any<string[]>());
        await alice.DidNotReceive().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Is(IrcNumerics.Format(IrcNumerics.RPL_MONONLINE)), Arg.Any<string[]>());
    }

    [Fact]
    public async Task NotifyOnline_SkipsUnregisteredSessions()
    {
        var watcher = CreateSession("Watcher", SessionState.Connecting);
        watcher.Info.MonitorList.Add("Alice");
        RegisterOnServer(watcher);

        var alice = CreateSession("Alice");
        RegisterOnServer(alice);

        await MonitorHandler.NotifyOnline(_server, alice);

        await watcher.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
        await watcher.DidNotReceive().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task NotifyOffline_SendsMonofflineToWatchers()
    {
        var watcher = CreateSession("Watcher");
        watcher.Info.MonitorList.Add("Alice");
        RegisterOnServer(watcher);

        await MonitorHandler.NotifyOffline(_server, "Alice");

        // NotifyOffline uses CapHelper.SendWithTimestamp -> SendMessageAsync (no server-time cap)
        await watcher.Received().SendMessageAsync(
            "test.server",
            IrcNumerics.Format(IrcNumerics.RPL_MONOFFLINE),
            Arg.Is<string[]>(p => p.Length >= 2 && p[1] == "Alice"));
    }

    [Fact]
    public async Task NotifyOffline_SkipsNonMonitoringSessions()
    {
        var watcher = CreateSession("Watcher");
        // Not monitoring anyone
        RegisterOnServer(watcher);

        await MonitorHandler.NotifyOffline(_server, "Alice");

        await watcher.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), Arg.Is(IrcNumerics.Format(IrcNumerics.RPL_MONOFFLINE)), Arg.Any<string[]>());
    }
}
