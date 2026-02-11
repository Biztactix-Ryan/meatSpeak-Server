namespace MeatSpeak.Server.Handlers.Operator;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;

public sealed class KillHandler : ICommandHandler
{
    public string Command => IrcConstants.KILL;
    public SessionState MinimumState => SessionState.Registered;

    public ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        // TODO: Implement KILL with permission system
        return ValueTask.CompletedTask;
    }
}
