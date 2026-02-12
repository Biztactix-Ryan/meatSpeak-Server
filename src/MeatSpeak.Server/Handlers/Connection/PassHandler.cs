namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class PassHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.PASS;
    public SessionState MinimumState => SessionState.Connecting;

    public PassHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (session.State >= SessionState.Registered)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_ALREADYREGISTRED,
                "You may not reregister");
            return;
        }
        if (await HandlerGuards.CheckNeedMoreParams(session, _server.Config.ServerName, message, 1, IrcConstants.PASS))
            return;
        session.Info.ServerPassword = message.GetParam(0);
    }
}
