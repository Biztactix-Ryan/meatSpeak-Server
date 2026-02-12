using System.Text;
using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.AdminApi.Auth;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;
using MeatSpeak.Server.Handlers.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace MeatSpeak.Server.Tests.Handlers;

public class AuthenticateHandlerTests
{
    private readonly IServer _server;
    private readonly IUserAccountRepository _accountRepo;
    private readonly AuthenticateHandler _handler;

    public AuthenticateHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _server.Events.Returns(Substitute.For<IEventBus>());

        _accountRepo = Substitute.For<IUserAccountRepository>();

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IUserAccountRepository)).Returns(_accountRepo);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        _handler = new AuthenticateHandler(_server, scopeFactory);
    }

    private ISession CreateSession(string nick, bool saslCap = true)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        if (saslCap)
            info.CapState.Acknowledged.Add("sasl");
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        return session;
    }

    private void SetupAccount(string username, string password)
    {
        var hash = PasswordHasher.HashPassword(password);
        _accountRepo.GetByAccountAsync(username, Arg.Any<CancellationToken>())
            .Returns(new UserAccountEntity
            {
                Id = Guid.NewGuid(),
                Account = username,
                PasswordHash = hash,
                CreatedAt = DateTimeOffset.UtcNow,
            });
    }

    [Fact]
    public async Task Authenticate_PLAIN_SendsPlus()
    {
        var session = CreateSession("User");
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { "PLAIN" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendMessageAsync(null, "AUTHENTICATE", Arg.Is<string[]>(p => p[0] == "+"));
    }

    [Fact]
    public async Task Authenticate_ValidCredentials_SetsAccountAndSendsSuccess()
    {
        var session = CreateSession("User");
        SetupAccount("testuser", "testpass");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("\0testuser\0testpass"));
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { credentials });

        await _handler.HandleAsync(session, msg);

        Assert.Equal("testuser", session.Info.Account);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_LOGGEDIN,
            Arg.Any<string[]>());
        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_SASLSUCCESS,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task Authenticate_WrongPassword_SendsSaslFail()
    {
        var session = CreateSession("User");
        SetupAccount("testuser", "correctpass");
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("\0testuser\0wrongpass"));
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { credentials });

        await _handler.HandleAsync(session, msg);

        Assert.Null(session.Info.Account);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_SASLFAIL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task Authenticate_UnknownAccount_SendsSaslFail()
    {
        var session = CreateSession("User");
        _accountRepo.GetByAccountAsync("unknown", Arg.Any<CancellationToken>())
            .Returns((UserAccountEntity?)null);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("\0unknown\0password"));
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { credentials });

        await _handler.HandleAsync(session, msg);

        Assert.Null(session.Info.Account);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_SASLFAIL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task Authenticate_InvalidBase64_SendsSaslFail()
    {
        var session = CreateSession("User");
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { "not-valid-base64!!!" });

        await _handler.HandleAsync(session, msg);

        Assert.Null(session.Info.Account);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_SASLFAIL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task Authenticate_MalformedPlain_SendsSaslFail()
    {
        var session = CreateSession("User");
        // Missing second null separator
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("just-text-no-nulls"));
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { credentials });

        await _handler.HandleAsync(session, msg);

        Assert.Null(session.Info.Account);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_SASLFAIL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task Authenticate_EmptyUsername_SendsSaslFail()
    {
        var session = CreateSession("User");
        // Empty username: authzid\0\0password
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("authz\0\0password"));
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { credentials });

        await _handler.HandleAsync(session, msg);

        Assert.Null(session.Info.Account);
        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_SASLFAIL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task Authenticate_WithoutSaslCap_SendsSaslFail()
    {
        var session = CreateSession("User", saslCap: false);
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { "PLAIN" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_SASLFAIL,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task Authenticate_AlreadyAuthenticated_SendsSaslAlready()
    {
        var session = CreateSession("User");
        session.Info.Account = "already_authed";
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { "PLAIN" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_SASLALREADY,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task Authenticate_Abort_SendsSaslAborted()
    {
        var session = CreateSession("User");
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { "*" });

        await _handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_SASLABORTED,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task Authenticate_WithAuthzid_SetsAuthcidAsAccount()
    {
        var session = CreateSession("User");
        SetupAccount("regularuser", "pass");
        // PLAIN with authzid: authzid\0authcid\0password
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("admin\0regularuser\0pass"));
        var msg = new IrcMessage(null, null, "AUTHENTICATE", new[] { credentials });

        await _handler.HandleAsync(session, msg);

        // Account should be set to authcid (regularuser), not authzid
        Assert.Equal("regularuser", session.Info.Account);
    }
}
