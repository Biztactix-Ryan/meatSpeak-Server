namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class WhoHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.WHO;
    public SessionState MinimumState => SessionState.Registered;

    public WhoHandler(IServer server) => _server = server;

    public ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        // TODO: Implement
        return ValueTask.CompletedTask;
    }
}
