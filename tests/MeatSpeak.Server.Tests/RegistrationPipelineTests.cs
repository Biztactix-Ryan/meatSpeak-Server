using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Registration;
using MeatSpeak.Server.Numerics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace MeatSpeak.Server.Tests;

public class RegistrationPipelineTests
{
    private readonly IServer _server;
    private readonly IEventBus _eventBus;
    private readonly DbWriteQueue _writeQueue;
    private readonly ServerMetrics _metrics;
    private readonly RegistrationPipeline _pipeline;
    private readonly ConcurrentDictionary<string, ISession> _sessions = new();

    public RegistrationPipelineTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig
        {
            ServerName = "test.server",
            NetworkName = "TestNet",
            Version = "test-1.0",
            Motd = new List<string> { "Welcome!" },
        });
        _server.StartedAt.Returns(DateTimeOffset.UtcNow);
        _server.ConnectionCount.Returns(1);
        _server.ChannelCount.Returns(0);
        _server.Sessions.Returns(_sessions);

        var modes = new ModeRegistry();
        modes.RegisterStandardModes();
        _server.Modes.Returns(modes);

        _eventBus = Substitute.For<IEventBus>();
        _server.Events.Returns(_eventBus);

        _writeQueue = new DbWriteQueue();
        _metrics = new ServerMetrics();

        var numerics = new NumericSender(_server);
        _pipeline = new RegistrationPipeline(_server, numerics, _writeQueue, null, NullLogger<RegistrationPipeline>.Instance, _metrics);
    }

    private ISession CreateSession(
        string? nick = "TestUser",
        string? user = "testuser",
        SessionState state = SessionState.Registering,
        bool capInNegotiation = false,
        bool capNegotiationComplete = false)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo
        {
            Nickname = nick,
            Username = user,
            Hostname = "host",
        };
        info.CapState.InNegotiation = capInNegotiation;
        info.CapState.NegotiationComplete = capNegotiationComplete;
        session.Info.Returns(info);
        session.Id.Returns("session-1");
        session.State.Returns(state);
        return session;
    }

    // --- No-op cases ---

    [Fact]
    public async Task TryCompleteRegistrationAsync_AlreadyRegistered_IsNoOp()
    {
        var session = CreateSession(state: SessionState.Registered);

        await _pipeline.TryCompleteRegistrationAsync(session);

        await session.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_MissingNickname_DoesNotRegister()
    {
        var session = CreateSession(nick: null);

        await _pipeline.TryCompleteRegistrationAsync(session);

        await session.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_MissingUsername_DoesNotRegister()
    {
        var session = CreateSession(user: null);

        await _pipeline.TryCompleteRegistrationAsync(session);

        await session.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_CapNegotiationInProgressAndNotComplete_DoesNotRegister()
    {
        var session = CreateSession(capInNegotiation: true, capNegotiationComplete: false);

        await _pipeline.TryCompleteRegistrationAsync(session);

        await session.DidNotReceive().SendNumericAsync(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string[]>());
    }

    // --- Successful registration cases ---

    [Fact]
    public async Task TryCompleteRegistrationAsync_CapNegotiationComplete_Registers()
    {
        var session = CreateSession(capInNegotiation: true, capNegotiationComplete: true);

        await _pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME, Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_NoCapNegotiation_RegistersNormally()
    {
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME, Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_SetsStateToRegistered()
    {
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_SendsWelcomeNumerics()
    {
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        // Welcome sequence: RPL_WELCOME, RPL_YOURHOST, RPL_CREATED, RPL_MYINFO
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME, Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_YOURHOST, Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_CREATED, Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MYINFO, Arg.Any<string[]>());
        // ISUPPORT
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ISUPPORT, Arg.Any<string[]>());
        // LUSERS
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LUSERCLIENT, Arg.Any<string[]>());
        // MOTD
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_MOTDSTART, Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_ENDOFMOTD, Arg.Any<string[]>());
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_PublishesSessionRegisteredEvent()
    {
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        _eventBus.Received().Publish(Arg.Is<SessionRegisteredEvent>(e =>
            e.SessionId == "session-1" && e.Nickname == "TestUser"));
    }

    [Fact]
    public async Task TryCompleteRegistrationAsync_WritesUserHistoryToQueue()
    {
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        Assert.True(_writeQueue.Reader.TryRead(out var item));
        var history = Assert.IsType<AddUserHistory>(item);
        Assert.Equal("TestUser", history.Entity.Nickname);
        Assert.Equal("testuser", history.Entity.Username);
        Assert.Equal("host", history.Entity.Hostname);
    }

    // --- Server-wide ban (K-line) tests ---

    private ISession CreateBanSession(string nick = "TestUser", string user = "testuser", string host = "host")
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = user, Hostname = host };
        session.Info.Returns(info);
        session.Id.Returns("session-ban");
        session.State.Returns(SessionState.Registering);
        return session;
    }

    private RegistrationPipeline CreatePipelineWithBans(params ServerBanEntity[] bans)
    {
        var banRepo = Substitute.For<IBanRepository>();
        banRepo.GetAllActiveAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<ServerBanEntity>>(bans.ToList()));

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var scope = Substitute.For<IServiceScope>();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IBanRepository)).Returns(banRepo);
        scope.ServiceProvider.Returns(serviceProvider);
        scopeFactory.CreateScope().Returns(scope);

        var numerics = new NumericSender(_server);
        return new RegistrationPipeline(_server, numerics, _writeQueue, scopeFactory, NullLogger<RegistrationPipeline>.Instance, _metrics);
    }

    // -- Basic matching --

    [Fact]
    public async Task KLine_HostWildcard_MatchesAndDisconnects()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@host", Reason = "Go away" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_YOUREBANNEDCREEP, Arg.Any<string[]>());
        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("Go away")));
        session.DidNotReceive().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_NoMatch_RegistersNormally()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@other.host" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME, Arg.Any<string[]>());
    }

    [Fact]
    public async Task KLine_EmptyBanList_RegistersNormally()
    {
        var pipeline = CreatePipelineWithBans();
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    // -- Wildcard patterns --

    [Fact]
    public async Task KLine_WildcardHost_MatchesSubdomain()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@*.evil.com", Reason = "Bad network" });
        var session = CreateBanSession(host: "box.evil.com");

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("Bad network")));
    }

    [Fact]
    public async Task KLine_WildcardHost_DoesNotMatchPartialDomain()
    {
        // *.evil.com should NOT match "notevil.com"
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@*.evil.com" });
        var session = CreateBanSession(host: "notevil.com");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_NickSpecific_MatchesThatNick()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "BadNick!*@*", Reason = "Nick banned" });
        var session = CreateBanSession(nick: "BadNick");

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("Nick banned")));
    }

    [Fact]
    public async Task KLine_NickSpecific_DoesNotMatchOtherNick()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "BadNick!*@*" });
        var session = CreateBanSession(nick: "GoodNick");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_UserSpecific_MatchesThatUser()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!spammer@*", Reason = "User banned" });
        var session = CreateBanSession(user: "spammer");

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("User banned")));
    }

    [Fact]
    public async Task KLine_UserSpecific_DoesNotMatchOtherUser()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!spammer@*" });
        var session = CreateBanSession(user: "legitimate");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_ExactMask_MatchesExactly()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "evil!bot@bad.host" });
        var session = CreateBanSession(nick: "evil", user: "bot", host: "bad.host");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.DidNotReceive().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_ExactMask_DoesNotMatchDifferentParts()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "evil!bot@bad.host" });
        var session = CreateBanSession(nick: "evil", user: "bot", host: "good.host");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_QuestionMarkWildcard_MatchesSingleChar()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@bad?.host" });
        var session = CreateBanSession(host: "bad1.host");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.DidNotReceive().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_QuestionMarkWildcard_DoesNotMatchMultipleChars()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@bad?.host" });
        var session = CreateBanSession(host: "bad12.host");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_CatchAll_MatchesEveryone()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@*", Reason = "Server locked" });
        var session = CreateBanSession(nick: "Anyone", user: "anything", host: "anywhere.com");

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("Server locked")));
    }

    [Fact]
    public async Task KLine_SingleStar_MatchesEverything()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        session.DidNotReceive().State = SessionState.Registered;
    }

    // -- Case insensitivity --

    [Fact]
    public async Task KLine_CaseInsensitive_MatchesRegardlessOfCase()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@EVIL.HOST" });
        var session = CreateBanSession(host: "evil.host");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.DidNotReceive().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_CaseInsensitive_NickMatchesRegardlessOfCase()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "BADNICK!*@*" });
        var session = CreateBanSession(nick: "badnick");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.DidNotReceive().State = SessionState.Registered;
    }

    // -- Multiple bans --

    [Fact]
    public async Task KLine_MultipleBans_FirstMatchWins()
    {
        var pipeline = CreatePipelineWithBans(
            new ServerBanEntity { Mask = "*!*@host", Reason = "First reason" },
            new ServerBanEntity { Mask = "*!*@host", Reason = "Second reason" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("First reason")));
        await session.DidNotReceive().DisconnectAsync(Arg.Is<string>(s => s.Contains("Second reason")));
    }

    [Fact]
    public async Task KLine_MultipleBans_SecondMatchUsedWhenFirstDoesNotMatch()
    {
        var pipeline = CreatePipelineWithBans(
            new ServerBanEntity { Mask = "*!*@other", Reason = "First" },
            new ServerBanEntity { Mask = "*!*@host", Reason = "Second" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("Second")));
    }

    [Fact]
    public async Task KLine_MultipleBans_NoneMatch_RegistersNormally()
    {
        var pipeline = CreatePipelineWithBans(
            new ServerBanEntity { Mask = "*!*@nope1" },
            new ServerBanEntity { Mask = "*!*@nope2" },
            new ServerBanEntity { Mask = "nope!*@*" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    // -- Ban reason handling --

    [Fact]
    public async Task KLine_NullReason_UsesDefaultMessage()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@host", Reason = null });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_YOUREBANNEDCREEP,
            Arg.Is<string[]>(a => a.Any(s => s.Contains("You are banned from this server"))));
        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("You are banned from this server")));
    }

    [Fact]
    public async Task KLine_EmptyReason_UsesDefaultMessage()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@host", Reason = "" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("You are banned from this server")));
    }

    [Fact]
    public async Task KLine_CustomReason_IncludedInDisconnectMessage()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@host", Reason = "Abuse detected: flooding" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.Received().DisconnectAsync(Arg.Is<string>(s => s.Contains("Abuse detected: flooding")));
    }

    // -- Side effects: banned sessions must NOT trigger post-registration actions --

    [Fact]
    public async Task KLine_Banned_DoesNotSendWelcomeNumerics()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@host" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.RPL_WELCOME, Arg.Any<string[]>());
    }

    [Fact]
    public async Task KLine_Banned_DoesNotPublishRegisteredEvent()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@host" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        _eventBus.DidNotReceive().Publish(Arg.Any<SessionRegisteredEvent>());
    }

    [Fact]
    public async Task KLine_Banned_DoesNotWriteUserHistory()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@host" });
        var session = CreateBanSession();

        await pipeline.TryCompleteRegistrationAsync(session);

        Assert.False(_writeQueue.Reader.TryRead(out _));
    }

    // -- Null scope factory (no DB) --

    [Fact]
    public async Task KLine_NullScopeFactory_SkipsBanCheckAndRegisters()
    {
        // The default _pipeline has null scopeFactory
        var session = CreateSession();

        await _pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    // -- IP address hostnames --

    [Fact]
    public async Task KLine_IPv4Wildcard_MatchesSubnet()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@192.168.1.*" });
        var session = CreateBanSession(host: "192.168.1.42");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.DidNotReceive().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_IPv4Wildcard_DoesNotMatchDifferentSubnet()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@192.168.1.*" });
        var session = CreateBanSession(host: "192.168.2.42");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_IPv6Host_MatchesExactly()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "*!*@::1" });
        var session = CreateBanSession(host: "::1");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.DidNotReceive().State = SessionState.Registered;
    }

    // -- Complex wildcard patterns --

    [Fact]
    public async Task KLine_MultipleWildcards_MatchesComplex()
    {
        // nick starts with "bot", any user, host ends with ".proxy.net"
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "bot*!*@*.proxy.net" });
        var session = CreateBanSession(nick: "bot-spam-123", user: "x", host: "node7.proxy.net");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.DidNotReceive().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_MultipleWildcards_DoesNotMatchWhenNickDiffers()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "bot*!*@*.proxy.net" });
        var session = CreateBanSession(nick: "human", user: "x", host: "node7.proxy.net");

        await pipeline.TryCompleteRegistrationAsync(session);

        session.Received().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_MixedQuestionAndStar_Matches()
    {
        // ? matches exactly one char, * matches the rest
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "?bot*!*@*" });

        var matchSession = CreateBanSession(nick: "xbotFoo");
        await pipeline.TryCompleteRegistrationAsync(matchSession);
        matchSession.DidNotReceive().State = SessionState.Registered;
    }

    [Fact]
    public async Task KLine_MixedQuestionAndStar_DoesNotMatchZeroCharsForQuestion()
    {
        var pipeline = CreatePipelineWithBans(new ServerBanEntity { Mask = "?bot*!*@*" });

        // "bot" has no char before it, so ? shouldn't match
        var noMatchSession = CreateBanSession(nick: "bot");
        await pipeline.TryCompleteRegistrationAsync(noMatchSession);
        noMatchSession.Received().State = SessionState.Registered;
    }
}
