namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;

public sealed class QuitHandler : ICommandHandler
{
    public string Command => IrcConstants.QUIT;
    public SessionState MinimumState => SessionState.Connecting;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        var reason = message.GetParam(0) ?? "Client quit";
        await session.DisconnectAsync(reason);
    }
}
