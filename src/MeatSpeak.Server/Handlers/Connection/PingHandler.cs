namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;

[FloodPenalty(0)]
public sealed class PingHandler : ICommandHandler
{
    public string Command => IrcConstants.PING;
    public SessionState MinimumState => SessionState.Connecting;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        var token = message.GetParam(0) ?? string.Empty;
        await session.SendMessageAsync(null, IrcConstants.PONG, token);
    }
}
