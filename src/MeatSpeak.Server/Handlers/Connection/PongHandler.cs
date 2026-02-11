namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;

public sealed class PongHandler : ICommandHandler
{
    public string Command => IrcConstants.PONG;
    public SessionState MinimumState => SessionState.Connecting;

    public ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        session.Info.LastActivity = DateTimeOffset.UtcNow;
        return ValueTask.CompletedTask;
    }
}
