using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Numerics;
using Microsoft.Extensions.Logging.Abstractions;

namespace MeatSpeak.Server.Tests.Handlers;

public class CapHandlerTests
{
    private readonly IServer _server;
    private readonly CapHandler _handler;

    public CapHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        var capRegistry = new CapabilityRegistry();
        _server.Capabilities.Returns(capRegistry);
        var numerics = new NumericSender(_server);
        var registration = new RegistrationPipeline(_server, numerics, NullLogger<RegistrationPipeline>.Instance);
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
}
