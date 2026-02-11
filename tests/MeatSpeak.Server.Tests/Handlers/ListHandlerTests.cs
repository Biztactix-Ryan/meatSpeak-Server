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

public class ListHandlerTests
{
    private readonly IServer _server;
    private readonly ListHandler _handler;
    private readonly Dictionary<string, IChannel> _channels;

    public ListHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _handler = new ListHandler(_server);
    }

    private ISession CreateSession(string nick)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        session.Info.Returns(info);
        return session;
    }

    [Fact]
    public async Task HandleAsync_ListsVisibleChannels()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", new ChannelMembership { Nickname = "Alice" });
        channel.Topic = "A topic";
        _channels["#test"] = channel;

        var session = CreateSession("Alice");
        var msg = new IrcMessage(null, null, "LIST", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LIST,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LISTEND,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_SkipsSecretChannelsForNonMembers()
    {
        var channel = new ChannelImpl("#secret");
        channel.Modes.Add('s');
        channel.AddMember("Other", new ChannelMembership { Nickname = "Other" });
        _channels["#secret"] = channel;

        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "LIST", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.RPL_LIST,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LISTEND,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_EmptyList_SendsOnlyListEnd()
    {
        var session = CreateSession("TestUser");
        var msg = new IrcMessage(null, null, "LIST", Array.Empty<string>());

        await _handler.HandleAsync(session, msg);

        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.RPL_LIST,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LISTEND,
            Arg.Any<string[]>());
    }
}
