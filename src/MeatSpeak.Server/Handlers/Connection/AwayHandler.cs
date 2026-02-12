namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class AwayHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.AWAY;
    public SessionState MinimumState => SessionState.Registered;

    public AwayHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        var awayMessage = message.GetParam(0);

        if (string.IsNullOrEmpty(awayMessage))
        {
            // Unset away
            session.Info.AwayMessage = null;
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_UNAWAY,
                "You are no longer marked as being away");
        }
        else
        {
            // Set away
            session.Info.AwayMessage = awayMessage;
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_NOWAWAY,
                "You have been marked as being away");
        }
    }
}
