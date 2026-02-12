using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Numerics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeatSpeak.Server.Tests.Handlers;

public class CapHandlerTests
{
    private readonly IServer _server;
    private readonly CapabilityRegistry _capRegistry;
    private readonly CapHandler _handler;

    public CapHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _capRegistry = new CapabilityRegistry();
        _capRegistry.Register(new SimpleCapability("server-time"));
        _capRegistry.Register(new SimpleCapability("echo-message"));
        _capRegistry.Register(new SimpleCapability("message-tags"));
        _server.Capabilities.Returns(_capRegistry);
        var numerics = new NumericSender(_server);
        var registration = new RegistrationPipeline(_server, numerics, null, null, NullLogger<RegistrationPipeline>.Instance, new ServerMetrics());
        _handler = new CapHandler(_server, registration);
    }

    [Fact]
    public async Task HandleAsync_CapLs_SendsCapList()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        var msg = new IrcMessage(null, null, "CAP", new[] { "LS" });

        await _handler.HandleAsync(session, msg);

        Assert.True(info.CapState.InNegotiation);
        await session.Received().SendMessageAsync(
            "test.server", "CAP", Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_CapEnd_CompletesNegotiation()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        session.State.Returns(SessionState.Connecting);
        var msg = new IrcMessage(null, null, "CAP", new[] { "END" });

        await _handler.HandleAsync(session, msg);

        Assert.True(info.CapState.NegotiationComplete);
    }

    [Fact]
    public async Task HandleAsync_CapReq_KnownCap_SendsAck()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "CAP", new[] { "REQ", "server-time" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "CAP",
            Arg.Is<string[]>(p => p[1] == "ACK" && p[2] == "server-time"));
    }

    [Fact]
    public async Task HandleAsync_CapReq_UnknownCap_SendsNak()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "CAP", new[] { "REQ", "nonexistent-cap" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "CAP",
            Arg.Is<string[]>(p => p[1] == "NAK" && p[2] == "nonexistent-cap"));
    }

    [Fact]
    public async Task HandleAsync_CapReq_MixedKnownUnknown_SendsBothAckAndNak()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "CAP", new[] { "REQ", "server-time nonexistent-cap" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "CAP",
            Arg.Is<string[]>(p => p[1] == "ACK"));
        await session.Received().SendMessageAsync("test.server", "CAP",
            Arg.Is<string[]>(p => p[1] == "NAK"));
    }

    [Fact]
    public async Task HandleAsync_CapReq_RemoveCap_SendsAck()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        info.CapState.Acknowledged.Add("server-time");
        session.Info.Returns(info);
        var msg = new IrcMessage(null, null, "CAP", new[] { "REQ", "-server-time" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "CAP",
            Arg.Is<string[]>(p => p[1] == "ACK" && p[2] == "-server-time"));
    }

    [Fact]
    public async Task HandleAsync_CapReq_RemoveUnknownCap_SendsNak()
    {
        var session = Substitute.For<ISession>();
        session.Info.Returns(new SessionInfo());
        var msg = new IrcMessage(null, null, "CAP", new[] { "REQ", "-nonexistent-cap" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "CAP",
            Arg.Is<string[]>(p => p[1] == "NAK"));
    }

    [Fact]
    public async Task HandleAsync_CapReq_AddsToAcknowledged()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        var msg = new IrcMessage(null, null, "CAP", new[] { "REQ", "server-time echo-message" });

        await _handler.HandleAsync(session, msg);

        Assert.Contains("server-time", info.CapState.Acknowledged);
        Assert.Contains("echo-message", info.CapState.Acknowledged);
    }

    [Fact]
    public async Task HandleAsync_CapReq_RemoveCap_RemovesFromAcknowledged()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        info.CapState.Acknowledged.Add("server-time");
        session.Info.Returns(info);
        var msg = new IrcMessage(null, null, "CAP", new[] { "REQ", "-server-time" });

        await _handler.HandleAsync(session, msg);

        Assert.DoesNotContain("server-time", info.CapState.Acknowledged);
    }

    [Fact]
    public async Task HandleAsync_CapList_ShowsAcknowledgedCaps()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        info.CapState.Acknowledged.Add("server-time");
        info.CapState.Acknowledged.Add("echo-message");
        session.Info.Returns(info);
        var msg = new IrcMessage(null, null, "CAP", new[] { "LIST" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "CAP",
            Arg.Is<string[]>(p => p[1] == "LIST" && p[2].Contains("server-time") && p[2].Contains("echo-message")));
    }

    [Fact]
    public async Task HandleAsync_CapList_EmptyWhenNothingAcked()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        var msg = new IrcMessage(null, null, "CAP", new[] { "LIST" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "CAP",
            Arg.Is<string[]>(p => p[1] == "LIST" && p[2] == ""));
    }

    [Fact]
    public async Task HandleAsync_CapLs_NicknameStarBeforeRegistration()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo(); // No nickname set
        session.Info.Returns(info);
        var msg = new IrcMessage(null, null, "CAP", new[] { "LS" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync("test.server", "CAP",
            Arg.Is<string[]>(p => p[0] == "*" && p[1] == "LS"));
    }

    [Fact]
    public async Task HandleAsync_NoParams_DoesNothing()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        var msg = new IrcMessage(null, null, "CAP", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        // Should silently return without sending anything
        await session.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_UnknownSubcommand_DoesNothing()
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo();
        session.Info.Returns(info);
        var msg = new IrcMessage(null, null, "CAP", new[] { "BLAH" });

        await _handler.HandleAsync(session, msg);

        // Unknown subcommand should not send any response (falls through switch)
        await session.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), "CAP", Arg.Any<string[]>());
    }
}
