namespace MeatSpeak.Server.Core.Commands;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;

public interface ICommandHandler
{
    string Command { get; }
    SessionState MinimumState { get; }
    ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default);
}
