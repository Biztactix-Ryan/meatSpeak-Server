namespace MeatSpeak.Server.Handlers.Operator;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class OperHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.OPER;
    public SessionState MinimumState => SessionState.Registered;

    public OperHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 2)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.OPER, "Not enough parameters");
            return;
        }

        var name = message.GetParam(0)!;
        var password = message.GetParam(1)!;

        if (string.IsNullOrEmpty(_server.Config.OperName) || string.IsNullOrEmpty(_server.Config.OperPassword))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOOPERHOST,
                "No O-lines for your host");
            return;
        }

        if (!string.Equals(name, _server.Config.OperName, StringComparison.Ordinal) ||
            !string.Equals(password, _server.Config.OperPassword, StringComparison.Ordinal))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_PASSWDMISMATCH,
                "Password incorrect");
            return;
        }

        session.Info.UserModes.Add('o');
        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_YOUREOPER,
            "You are now an IRC operator");
        await session.SendMessageAsync(session.Info.Prefix, IrcConstants.MODE, session.Info.Nickname!, "+o");
    }
}
