namespace MeatSpeak.Server.Handlers.Operator;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Permissions;

[RequiresServerPermission(ServerPermission.ManageServer)]
public sealed class RehashHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly IConfigReloader? _reloader;
    public string Command => IrcConstants.REHASH;
    public SessionState MinimumState => SessionState.Registered;

    public RehashHandler(IServer server, IConfigReloader? reloader = null)
    {
        _server = server;
        _reloader = reloader;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (!PermissionResolution.HasServerPermission(session.CachedServerPermissions, ServerPermission.ManageServer))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOPRIVILEGES,
                "Permission Denied- You do not have the required server permission");
            return;
        }

        if (_reloader != null)
            await _reloader.ReloadAsync(ct);

        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_YOUREOPER,
            "Server configuration reloaded");
    }
}
