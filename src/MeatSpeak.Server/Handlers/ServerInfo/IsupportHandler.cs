namespace MeatSpeak.Server.Handlers.ServerInfo;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Numerics;

public sealed class IsupportHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly NumericSender _numerics;
    // No standard IRC command for ISUPPORT request, but we handle VERSION for re-sending
    // This exists for completeness; ISUPPORT is sent during registration
    public string Command => "ISUPPORT"; // Non-standard, internal only
    public SessionState MinimumState => SessionState.Registered;

    public IsupportHandler(IServer server, NumericSender numerics)
    {
        _server = server;
        _numerics = numerics;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        await _numerics.SendIsupportAsync(session);
    }
}
