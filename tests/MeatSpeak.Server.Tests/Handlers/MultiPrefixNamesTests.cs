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

public class MultiPrefixNamesTests
{
    private readonly IServer _server;
    private readonly Dictionary<string, IChannel> _channels;

    public MultiPrefixNamesTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
    }

    private ISession CreateSession(string nick, bool multiPrefix = false, bool userhostInNames = false)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        if (multiPrefix)
            info.CapState.Acknowledged.Add("multi-prefix");
        if (userhostInNames)
            info.CapState.Acknowledged.Add("userhost-in-names");
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task Names_WithMultiPrefix_ShowsAllPrefixes()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("OpVoice", new ChannelMembership { Nickname = "OpVoice", IsOperator = true, HasVoice = true });
        channel.AddMember("Regular", new ChannelMembership { Nickname = "Regular" });
        _channels["#test"] = channel;

        // Create sessions before setting up Returns to avoid NSubstitute nesting issues
        var opVoiceSession = CreateSession("OpVoice");
        var regularSession = CreateSession("Regular");
        var requester = CreateSession("Requester", multiPrefix: true);

        await JoinHandler.SendNamesReply(requester, channel, "test.server", _server);

        // OpVoice should have @+ prefix (both op and voice)
        await requester.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Is<string[]>(p => p[2].Contains("@+OpVoice")));
    }

    [Fact]
    public async Task Names_WithoutMultiPrefix_ShowsHighestOnly()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("OpVoice", new ChannelMembership { Nickname = "OpVoice", IsOperator = true, HasVoice = true });
        _channels["#test"] = channel;

        var requester = CreateSession("Requester"); // no multi-prefix

        await JoinHandler.SendNamesReply(requester, channel, "test.server", _server);

        // OpVoice should only have @ prefix (highest)
        await requester.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Is<string[]>(p => p[2].Contains("@OpVoice") && !p[2].Contains("@+OpVoice")));
    }

    [Fact]
    public async Task Names_WithUserhostInNames_ShowsFullHostmask()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Nick", new ChannelMembership { Nickname = "Nick" });
        _channels["#test"] = channel;

        var nickSession = CreateSession("Nick");
        var requester = CreateSession("Requester", userhostInNames: true);

        await JoinHandler.SendNamesReply(requester, channel, "test.server", _server);

        await requester.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Is<string[]>(p => p[2].Contains("Nick!user@host")));
    }

    [Fact]
    public async Task Names_WithoutUserhostInNames_ShowsNickOnly()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Nick", new ChannelMembership { Nickname = "Nick" });
        _channels["#test"] = channel;

        var requester = CreateSession("Requester"); // no userhost-in-names

        await JoinHandler.SendNamesReply(requester, channel, "test.server", _server);

        await requester.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Is<string[]>(p => p[2] == "Nick" || p[2].Contains("Nick")));
        // Should NOT contain "!" (hostmask format)
        await requester.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Is<string[]>(p => !p[2].Contains("!")));
    }

    [Fact]
    public async Task Names_WithBothCaps_ShowsAllPrefixesAndHostmask()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true, HasVoice = true });
        _channels["#test"] = channel;

        var opSession = CreateSession("Op");
        var requester = CreateSession("Requester", multiPrefix: true, userhostInNames: true);

        await JoinHandler.SendNamesReply(requester, channel, "test.server", _server);

        await requester.Received().SendNumericAsync("test.server", IrcNumerics.RPL_NAMREPLY,
            Arg.Is<string[]>(p => p[2].Contains("@+Op!user@host")));
    }
}
