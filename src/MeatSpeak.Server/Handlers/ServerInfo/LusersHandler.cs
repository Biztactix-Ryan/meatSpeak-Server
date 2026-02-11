namespace MeatSpeak.Server.Handlers.ServerInfo;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Numerics;

public sealed class LusersHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly NumericSender _numerics;
    public string Command => IrcConstants.LUSERS;
    public SessionState MinimumState => SessionState.Registered;

    public LusersHandler(IServer server, NumericSender numerics)
    {
        _server = server;
        _numerics = numerics;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        await _numerics.SendLusersAsync(session);
    }
}
