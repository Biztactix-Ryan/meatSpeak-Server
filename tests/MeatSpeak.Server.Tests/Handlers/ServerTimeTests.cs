using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Handlers.Messaging;
using MeatSpeak.Server.Handlers.Channels;
using MeatSpeak.Server.Handlers.Connection;
using MeatSpeak.Server.Numerics;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.State;
using Microsoft.Extensions.Logging;

namespace MeatSpeak.Server.Tests.Handlers;

public class ServerTimeTests
{
    private readonly IServer _server;
    private readonly Dictionary<string, IChannel> _channels;

    public ServerTimeTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());
        _channels = new Dictionary<string, IChannel>(StringComparer.OrdinalIgnoreCase);
        _server.Channels.Returns(_channels);
        _server.FindSessionByNick(Arg.Any<string>()).Returns((ISession?)null);
    }

    private ISession CreateSession(string nick, bool serverTime = false)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        if (serverTime)
            info.CapState.Acknowledged.Add("server-time");
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        _server.FindSessionByNick(nick).Returns(session);
        return session;
    }

    [Fact]
    public async Task Privmsg_WithServerTimeCap_SendsTaggedMessage()
    {
        var sender = CreateSession("Sender");
        var target = CreateSession("Target", serverTime: true);
        var handler = new PrivmsgHandler(_server);
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "Target", "Hello" });

        await handler.HandleAsync(sender, msg);

        await target.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
        await target.DidNotReceive().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Privmsg_WithoutServerTimeCap_SendsRegularMessage()
    {
        var sender = CreateSession("Sender");
        var target = CreateSession("Target", serverTime: false);
        var handler = new PrivmsgHandler(_server);
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "Target", "Hello" });

        await handler.HandleAsync(sender, msg);

        await target.Received().SendMessageAsync(
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
        await target.DidNotReceive().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task ChannelPrivmsg_WithServerTimeCap_SendsTaggedToMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Sender", new ChannelMembership { Nickname = "Sender" });
        channel.AddMember("Viewer", new ChannelMembership { Nickname = "Viewer" });
        _channels["#test"] = channel;

        var sender = CreateSession("Sender");
        var viewer = CreateSession("Viewer", serverTime: true);
        var handler = new PrivmsgHandler(_server);
        var msg = new IrcMessage(null, null, "PRIVMSG", new[] { "#test", "Hello channel" });

        await handler.HandleAsync(sender, msg);

        await viewer.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "PRIVMSG", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Part_WithServerTimeCap_SendsTaggedMessage()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Leaver", new ChannelMembership { Nickname = "Leaver" });
        channel.AddMember("Stayer", new ChannelMembership { Nickname = "Stayer" });
        _channels["#test"] = channel;

        var leaver = CreateSession("Leaver");
        leaver.Info.Channels.Add("#test");
        var stayer = CreateSession("Stayer", serverTime: true);
        var handler = new PartHandler(_server);
        var msg = new IrcMessage(null, null, "PART", new[] { "#test", "Bye" });

        await handler.HandleAsync(leaver, msg);

        await stayer.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "PART", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Topic_WithServerTimeCap_SendsTaggedMessage()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("Viewer", new ChannelMembership { Nickname = "Viewer" });
        _channels["#test"] = channel;

        var op = CreateSession("Op");
        op.Info.Channels.Add("#test");
        var viewer = CreateSession("Viewer", serverTime: true);
        var handler = new TopicHandler(_server);
        var msg = new IrcMessage(null, null, "TOPIC", new[] { "#test", "New topic" });

        await handler.HandleAsync(op, msg);

        await viewer.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "TOPIC", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Kick_WithServerTimeCap_SendsTaggedMessage()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("Victim", new ChannelMembership { Nickname = "Victim" });
        channel.AddMember("Viewer", new ChannelMembership { Nickname = "Viewer" });
        _channels["#test"] = channel;

        var op = CreateSession("Op");
        var victim = CreateSession("Victim");
        victim.Info.Channels.Add("#test");
        var viewer = CreateSession("Viewer", serverTime: true);
        var handler = new KickHandler(_server);
        var msg = new IrcMessage(null, null, "KICK", new[] { "#test", "Victim", "Reason" });

        await handler.HandleAsync(op, msg);

        await viewer.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "KICK", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Notice_WithServerTimeCap_SendsTaggedMessage()
    {
        var sender = CreateSession("Sender");
        var target = CreateSession("Target", serverTime: true);
        var handler = new NoticeHandler(_server);
        var msg = new IrcMessage(null, null, "NOTICE", new[] { "Target", "Notice text" });

        await handler.HandleAsync(sender, msg);

        await target.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "NOTICE", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Join_WithServerTimeCap_SendsTaggedMessage()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Existing", new ChannelMembership { Nickname = "Existing" });
        _channels["#test"] = channel;
        _server.GetOrCreateChannel("#test").Returns(channel);

        var existing = CreateSession("Existing", serverTime: true);
        var joiner = CreateSession("Joiner");

        var handler = new JoinHandler(_server);
        var msg = new IrcMessage(null, null, "JOIN", new[] { "#test" });
        await handler.HandleAsync(joiner, msg);

        await existing.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "JOIN", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Nick_WithServerTimeCap_SendsTaggedToSelf()
    {
        var session = CreateSession("OldNick", serverTime: true);
        session.State.Returns(SessionState.Registered);

        var numerics = new NumericSender(_server);
        var logger = Substitute.For<ILogger<RegistrationPipeline>>();
        var metrics = new ServerMetrics();
        var registration = new RegistrationPipeline(_server, numerics, null, logger, metrics);
        var handler = new NickHandler(_server, registration);
        var msg = new IrcMessage(null, null, "NICK", new[] { "NewNick" });

        await handler.HandleAsync(session, msg);

        await session.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "NICK", Arg.Any<string[]>());
    }

    [Fact]
    public async Task Nick_WithServerTimeCap_SendsTaggedToChannelMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Changer", new ChannelMembership { Nickname = "Changer" });
        channel.AddMember("Viewer", new ChannelMembership { Nickname = "Viewer" });
        _channels["#test"] = channel;

        var changer = CreateSession("Changer");
        changer.State.Returns(SessionState.Registered);
        changer.Info.Channels.Add("#test");
        var viewer = CreateSession("Viewer", serverTime: true);

        var numerics = new NumericSender(_server);
        var logger = Substitute.For<ILogger<RegistrationPipeline>>();
        var metrics = new ServerMetrics();
        var registration = new RegistrationPipeline(_server, numerics, null, logger, metrics);
        var handler = new NickHandler(_server, registration);
        var msg = new IrcMessage(null, null, "NICK", new[] { "NewNick" });

        await handler.HandleAsync(changer, msg);

        await viewer.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "NICK",
            Arg.Is<string[]>(p => p[0] == "NewNick"));
    }

    [Fact]
    public async Task Mode_ChannelBroadcast_WithServerTimeCap_SendsTaggedMessage()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Op", new ChannelMembership { Nickname = "Op", IsOperator = true });
        channel.AddMember("Viewer", new ChannelMembership { Nickname = "Viewer" });
        _channels["#test"] = channel;

        var modes = new MeatSpeak.Server.Core.Modes.ModeRegistry();
        modes.RegisterChannelMode(new MeatSpeak.Server.Core.Modes.ModeDefinition('n', MeatSpeak.Server.Core.Modes.ModeType.D, "no-external"));
        _server.Modes.Returns(modes);

        var op = CreateSession("Op");
        var viewer = CreateSession("Viewer", serverTime: true);
        var handler = new ModeHandler(_server);
        var msg = new IrcMessage(null, null, "MODE", new[] { "#test", "+n" });

        await handler.HandleAsync(op, msg);

        await viewer.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "MODE", Arg.Any<string[]>());
    }

    [Fact]
    public async Task JoinZero_PartBroadcast_WithServerTimeCap_SendsTaggedMessage()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Leaver", new ChannelMembership { Nickname = "Leaver" });
        channel.AddMember("Stayer", new ChannelMembership { Nickname = "Stayer" });
        _channels["#test"] = channel;

        var leaver = CreateSession("Leaver");
        leaver.Info.Channels.Add("#test");
        var stayer = CreateSession("Stayer", serverTime: true);

        var handler = new JoinHandler(_server);
        var msg = new IrcMessage(null, null, "JOIN", new[] { "0" });

        await handler.HandleAsync(leaver, msg);

        await stayer.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            Arg.Any<string>(), "PART", Arg.Any<string[]>());
    }
}
