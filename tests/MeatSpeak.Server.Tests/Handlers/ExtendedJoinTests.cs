using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Handlers.Channels;
using MeatSpeak.Server.State;

namespace MeatSpeak.Server.Tests.Handlers;

public class ExtendedJoinTests
{
    private readonly IServer _server;
    private readonly Dictionary<string, IChannel> _channels;

    public ExtendedJoinTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
    }

    private ISession CreateSession(string nick, bool extendedJoin = false)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host", Realname = "Real Name" };
        if (extendedJoin)
            info.CapState.Acknowledged.Add("extended-join");
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task Join_WithExtendedJoinCap_SendsAccountAndRealname()
    {
        var existingChannel = new ChannelImpl("#test");
        existingChannel.AddMember("Existing", new ChannelMembership { Nickname = "Existing" });
        _channels["#test"] = existingChannel;
        _server.GetOrCreateChannel("#test").Returns(existingChannel);

        var existing = CreateSession("Existing", extendedJoin: true);
        var joiner = CreateSession("Joiner");
        joiner.Info.Account = "joiner_account";
        joiner.Info.Realname = "Joiner Real";

        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });
        var handler = new JoinHandler(_server);
        await handler.HandleAsync(joiner, msg);

        // Existing member with extended-join should get account+realname
        await existing.Received().SendMessageAsync(
            Arg.Any<string>(), "JOIN",
            Arg.Is<string[]>(p => p.Length == 3 && p[0] == "#test" && p[1] == "joiner_account" && p[2] == "Joiner Real"));
    }

    [Fact]
    public async Task Join_WithExtendedJoin_NoAccount_SendsStar()
    {
        var existingChannel = new ChannelImpl("#test");
        existingChannel.AddMember("Existing", new ChannelMembership { Nickname = "Existing" });
        _channels["#test"] = existingChannel;
        _server.GetOrCreateChannel("#test").Returns(existingChannel);

        var existing = CreateSession("Existing", extendedJoin: true);
        var joiner = CreateSession("Joiner");
        // Account is null, should send "*"

        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });
        var handler = new JoinHandler(_server);
        await handler.HandleAsync(joiner, msg);

        await existing.Received().SendMessageAsync(
            Arg.Any<string>(), "JOIN",
            Arg.Is<string[]>(p => p.Length == 3 && p[1] == "*"));
    }

    [Fact]
    public async Task Join_WithoutExtendedJoin_SendsNormalJoin()
    {
        var existingChannel = new ChannelImpl("#test");
        existingChannel.AddMember("Existing", new ChannelMembership { Nickname = "Existing" });
        _channels["#test"] = existingChannel;
        _server.GetOrCreateChannel("#test").Returns(existingChannel);

        var existing = CreateSession("Existing"); // No extended-join cap
        var joiner = CreateSession("Joiner");

        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });
        var handler = new JoinHandler(_server);
        await handler.HandleAsync(joiner, msg);

        await existing.Received().SendMessageAsync(
            Arg.Any<string>(), "JOIN",
            Arg.Is<string[]>(p => p.Length == 1 && p[0] == "#test"));
    }
}
