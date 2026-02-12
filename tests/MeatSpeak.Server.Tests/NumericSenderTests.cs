using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Numerics;

namespace MeatSpeak.Server.Tests;

public class NumericSenderTests
{
    private readonly IServer _server;
    private readonly NumericSender _sender;
    private readonly ServerConfig _config;

    public NumericSenderTests()
    {
        _server = Substitute.For<IServer>();
        _config = new ServerConfig
        {
            ServerName = "test.server",
            NetworkName = "TestNet",
            Version = "test-1.0",
            Motd = new List<string> { "Welcome!", "Have fun!" },
        };
        _server.Config.Returns(_config);
        _server.StartedAt.Returns(DateTimeOffset.UtcNow);
        _server.ConnectionCount.Returns(42);
        _server.ChannelCount.Returns(5);

        var modes = new ModeRegistry();
        modes.RegisterStandardModes();
        _server.Modes.Returns(modes);

        _sender = new NumericSender(_server);
    }

    private ISession CreateSession(string nick = "TestUser")
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo
        {
            Nickname = nick,
            Username = "user",
            Hostname = "host",
        };
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        return session;
    }

    // --- SendWelcomeAsync ---

    [Fact]
    public async Task SendWelcomeAsync_SendsRplWelcome()
    {
        var session = CreateSession();

        await _sender.SendWelcomeAsync(session);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME,
            Arg.Is<string[]>(p => p.Length == 1 && p[0].Contains("TestNet") && p[0].Contains("TestUser!user@host")));
    }

    [Fact]
    public async Task SendWelcomeAsync_SendsRplYourhost()
    {
        var session = CreateSession();

        await _sender.SendWelcomeAsync(session);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_YOURHOST,
            Arg.Is<string[]>(p => p.Length == 1 && p[0].Contains("test.server") && p[0].Contains("test-1.0")));
    }

    [Fact]
    public async Task SendWelcomeAsync_SendsRplCreated()
    {
        var session = CreateSession();

        await _sender.SendWelcomeAsync(session);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_CREATED,
            Arg.Is<string[]>(p => p.Length == 1 && p[0].Contains("This server was created")));
    }

    [Fact]
    public async Task SendWelcomeAsync_SendsRplMyinfo()
    {
        var session = CreateSession();

        await _sender.SendWelcomeAsync(session);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MYINFO,
            Arg.Is<string[]>(p => p.Length == 1 && p[0].Contains("test.server") && p[0].Contains("test-1.0")));
    }

    // --- SendIsupportAsync ---

    [Fact]
    public async Task SendIsupportAsync_SendsRplIsupport()
    {
        var session = CreateSession();

        await _sender.SendIsupportAsync(session);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ISUPPORT,
            Arg.Is<string[]>(p =>
                p.Any(t => t.StartsWith("NETWORK=TestNet")) &&
                p.Any(t => t.StartsWith("CHANMODES=")) &&
                p.Any(t => t == "MONITOR=100") &&
                p.Any(t => t == "are supported by this server")));
    }

    // --- SendMotdAsync ---

    [Fact]
    public async Task SendMotdAsync_SendsMotdStartBodyAndEnd()
    {
        var session = CreateSession();

        await _sender.SendMotdAsync(session);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MOTDSTART,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MOTD,
            Arg.Is<string[]>(p => p[0].Contains("Welcome!")));
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MOTD,
            Arg.Is<string[]>(p => p[0].Contains("Have fun!")));
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFMOTD,
            Arg.Any<string[]>());
    }

    // --- SendLusersAsync ---

    [Fact]
    public async Task SendLusersAsync_SendsAllLuserNumerics()
    {
        var session = CreateSession();

        await _sender.SendLusersAsync(session);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LUSERCLIENT,
            Arg.Is<string[]>(p => p[0].Contains("42")));
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LUSEROP,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LUSERUNKNOWN,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LUSERCHANNELS,
            Arg.Is<string[]>(p => p[0] == "5"));
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LUSERME,
            Arg.Is<string[]>(p => p[0].Contains("42")));
    }
}
