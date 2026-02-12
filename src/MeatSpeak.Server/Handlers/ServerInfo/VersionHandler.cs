namespace MeatSpeak.Server.Handlers.ServerInfo;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

[FloodPenalty(1)]
public sealed class VersionHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.VERSION;
    public SessionState MinimumState => SessionState.Registered;

    public VersionHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_VERSION,
            _server.Config.Version, _server.Config.ServerName, "MeatSpeak IRC Server");
    }
}
