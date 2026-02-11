namespace MeatSpeak.Server.Handlers.Operator;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;

public sealed class OperHandler : ICommandHandler
{
    public string Command => IrcConstants.OPER;
    public SessionState MinimumState => SessionState.Registered;

    public ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        // TODO: Implement OPER with permission system
        return ValueTask.CompletedTask;
    }
}
