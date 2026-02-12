using Xunit;
using NSubstitute;
using MeatSpeak.Protocol;
using IrcNumerics = MeatSpeak.Protocol.Numerics;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Handlers.Operator;
using MeatSpeak.Server.Permissions;

namespace MeatSpeak.Server.Tests.Handlers;

public class RehashHandlerTests
{
    private readonly IServer _server;
    private readonly IConfigReloader _reloader;

    public RehashHandlerTests()
    {
        _server = Substitute.For<IServer>();
        _server.Config.Returns(new ServerConfig { ServerName = "test.server" });
        _reloader = Substitute.For<IConfigReloader>();
    }

    private ISession CreateSession(string nick, ServerPermission permissions = ServerPermission.None)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = nick, Username = "user", Hostname = "host" };
        session.Info.Returns(info);
        session.Id.Returns(nick + "-id");
        session.CachedServerPermissions.Returns(permissions);
        return session;
    }

    [Fact]
    public async Task HandleAsync_WithoutManageServer_SendsErrNoPrivileges()
    {
        var handler = new RehashHandler(_server, _reloader);
        var session = CreateSession("User", ServerPermission.None);
        var msg = new IrcMessage(null, null, "REHASH", Array.Empty<string>());

        await handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.ERR_NOPRIVILEGES,
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task HandleAsync_WithoutManageServer_DoesNotCallReloader()
    {
        var handler = new RehashHandler(_server, _reloader);
        var session = CreateSession("User", ServerPermission.None);
        var msg = new IrcMessage(null, null, "REHASH", Array.Empty<string>());

        await handler.HandleAsync(session, msg);

        await _reloader.DidNotReceive().ReloadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithManageServer_CallsReloadAsync()
    {
        var handler = new RehashHandler(_server, _reloader);
        var session = CreateSession("Admin", ServerPermission.ManageServer);
        var msg = new IrcMessage(null, null, "REHASH", Array.Empty<string>());

        await handler.HandleAsync(session, msg);

        await _reloader.Received().ReloadAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithManageServer_SendsSuccessReply()
    {
        var handler = new RehashHandler(_server, _reloader);
        var session = CreateSession("Admin", ServerPermission.ManageServer);
        var msg = new IrcMessage(null, null, "REHASH", Array.Empty<string>());

        await handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_YOUREOPER,
            Arg.Is<string[]>(p => p[0] == "Server configuration reloaded"));
    }

    [Fact]
    public async Task HandleAsync_WithManageServer_NullReloader_StillSendsSuccess()
    {
        var handler = new RehashHandler(_server, reloader: null);
        var session = CreateSession("Admin", ServerPermission.ManageServer);
        var msg = new IrcMessage(null, null, "REHASH", Array.Empty<string>());

        await handler.HandleAsync(session, msg);

        await session.Received().SendNumericAsync("test.server", IrcNumerics.RPL_YOUREOPER,
            Arg.Is<string[]>(p => p[0] == "Server configuration reloaded"));
    }

    [Fact]
    public async Task HandleAsync_WithManageServerFlag_PermissionCheckPasses()
    {
        var handler = new RehashHandler(_server, _reloader);
        // Combine ManageServer with another flag to verify the flag-based check works
        var session = CreateSession("Admin", ServerPermission.ManageServer | ServerPermission.ViewAuditLog);
        var msg = new IrcMessage(null, null, "REHASH", Array.Empty<string>());

        await handler.HandleAsync(session, msg);

        await session.DidNotReceive().SendNumericAsync("test.server", IrcNumerics.ERR_NOPRIVILEGES,
            Arg.Any<string[]>());
        await _reloader.Received().ReloadAsync(Arg.Any<CancellationToken>());
    }
}
