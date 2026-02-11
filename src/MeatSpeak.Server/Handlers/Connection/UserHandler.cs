namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Registration;

public sealed class UserHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly RegistrationPipeline _registration;
    public string Command => IrcConstants.USER;
    public SessionState MinimumState => SessionState.Connecting;

    public UserHandler(IServer server, RegistrationPipeline registration)
    {
        _server = server;
        _registration = registration;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (session.State >= SessionState.Registered)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_ALREADYREGISTRED,
                "You may not reregister");
            return;
        }
        if (message.Parameters.Count < 4)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.USER, "Not enough parameters");
            return;
        }

        session.Info.Username = message.GetParam(0);
        session.Info.Realname = message.GetParam(3); // params: username mode unused :realname

        if (session.State < SessionState.Registering)
            session.State = SessionState.Registering;
        await _registration.TryCompleteRegistrationAsync(session);
    }
}
