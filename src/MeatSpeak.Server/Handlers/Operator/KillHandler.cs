namespace MeatSpeak.Server.Handlers.Operator;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class KillHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.KILL;
    public SessionState MinimumState => SessionState.Registered;

    public KillHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (await HandlerGuards.CheckNeedMoreParams(session, _server.Config.ServerName, message, 2, IrcConstants.KILL))
            return;

        // Require IRC operator
        if (!session.Info.UserModes.Contains('o'))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOPRIVILEGES,
                "Permission Denied- You're not an IRC operator");
            return;
        }

        var targetNick = message.GetParam(0)!;
        var reason = message.GetParam(1)!;

        var targetSession = _server.FindSessionByNick(targetNick);
        if (targetSession == null)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHNICK,
                targetNick, "No such nick/channel");
            return;
        }

        var killReason = $"Killed ({session.Info.Nickname} ({reason}))";

        // Broadcast QUIT to all channels the target was in
        await ChannelBroadcaster.BroadcastAcrossChannels(
            _server, targetSession.Info.Channels.ToList(), targetNick,
            targetSession.Info.Prefix, IrcConstants.QUIT, killReason);

        await targetSession.DisconnectAsync(killReason);
    }
}
