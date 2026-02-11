using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Operator;
using MeatSpeak.Server.State;

namespace MeatSpeak.Server.Tests.Handlers;

public class KillHandlerTests
{
    private readonly IServer _server;
    private readonly KillHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public KillHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
        _handler = new KillHandler(_server);
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
    public async Task HandleAsync_NoParams_SendsNeedMoreParams()
    {
        var session = CreateSession("Op");
        session.Info.UserModes.Add('o');
        var msg = new IrcMessage(null, null, "KILL", new[] { "Target" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NEEDMOREPARAMS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NotOperator_SendsNoPrivileges()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "KILL", new[] { "Target", "reason" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOPRIVILEGES,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_TargetNotFound_SendsError()
    {
        var session = CreateSession("Op");
        session.Info.UserModes.Add('o');
        var msg = new IrcMessage(null, null, "KILL", new[] { "NonExistent", "reason" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOSUCHNICK,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_ValidKill_DisconnectsTarget()
    {
        var session = CreateSession("Op");
        session.Info.UserModes.Add('o');
        var target = CreateSession("Target");
        var msg = new IrcMessage(null, null, "KILL", new[] { "Target", "Spamming" });

        await _handler.HandleAsync(session, msg);

        await target.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("Spamming")));
    }

    [Fact]
    public async Task HandleAsync_ValidKill_BroadcastsQuitToChannelMembers()
    {
        var session = CreateSession("Op");
        session.Info.UserModes.Add('o');
        var target = CreateSession("Target");
        target.Info.Channels.Add("#test");

        var channel = new ChannelImpl("#test");
        channel.AddMember("Target", new ChannelMembership { Nickname = "Target" });
        channel.AddMember("Bystander", new ChannelMembership { Nickname = "Bystander" });
        _channels["#test"] = channel;

        var bystander = CreateSession("Bystander");
        var msg = new IrcMessage(null, null, "KILL", new[] { "Target", "reason" });

        await _handler.HandleAsync(session, msg);

        await bystander.Received().SendMessageAsync(
            Arg.Any<string>(), "QUIT", Arg.Any<string[]>());
    }
}
