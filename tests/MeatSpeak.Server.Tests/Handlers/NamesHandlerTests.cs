using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Channels;
using MeatSpeak.Server.State;

namespace MeatSpeak.Server.Tests.Handlers;

public class NamesHandlerTests
{
    private readonly IServer _server;
    private readonly NamesHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public NamesHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _handler = new NamesHandler(_server);
    }

    private ISession CreateSession(string nick)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        session.Info.Returns(info);
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task HandleAsync_SpecificChannel_SendsNamesReply()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice", IsOperator = true });
        channel.AddMember("Bob", new ChannelMembership { Nickname = "Bob" });
        _channels["#test"] = channel;

        var session = CreateSession("Alice");
        var msg = new IrcMessage(null, null, "NAMES", new[] { "#test" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFNAMES,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NonexistentChannel_SendsEndOfNames()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "NAMES", new[] { "#nonexistent" });

        await _handler.HandleAsync(session, msg);

        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFNAMES,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_NoChannel_ListsAll()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice" });
        _channels["#test"] = channel;

        var session = CreateSession("Alice");
        var msg = new IrcMessage(null, null, "NAMES", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_SecretChannel_SkipsForNonMember()
    {
        var channel = new ChannelImpl("#secret");
        channel.Modes.Add('s');
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#secret"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "NAMES", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        // Should only receive the final end-of-names for *, not RPL_NAMREPLY for #secret
        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Any<string[]>());
    }
}
