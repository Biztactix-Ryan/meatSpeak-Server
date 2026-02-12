namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

[FloodPenalty(2)]
public sealed class InviteHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.INVITE;
    public SessionState MinimumState => SessionState.Registered;

    public InviteHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (await HandlerGuards.CheckNeedMoreParams(session, _server.Config.ServerName, message, 2, IrcConstants.INVITE))
            return;

        var targetNick = message.GetParam(0)!;
        var channelName = message.GetParam(1)!;

        // Check target exists
        var targetSession = _server.FindSessionByNick(targetNick);
        if (targetSession == null)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHNICK,
                targetNick, "No such nick/channel");
            return;
        }

        if (_server.Channels.TryGetValue(channelName, out var channel))
        {
            // Check if target is already on channel
            if (channel.IsMember(targetNick))
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_USERONCHANNEL,
                    targetNick, channelName, "is already on channel");
                return;
            }

            // If channel is invite-only, inviter must be chanop
            if (channel.Modes.Contains('i'))
            {
                if (await HandlerGuards.CheckChanOpPrivsNeeded(session, _server.Config.ServerName, channel, session.Info.Nickname!))
                    return;
            }

            channel.AddInvite(targetNick);
        }

        // Send RPL_INVITING to inviter
        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_INVITING,
            targetNick, channelName);

        // Send INVITE message to target
        await targetSession.SendMessageAsync(session.Info.Prefix, IrcConstants.INVITE, targetNick, channelName);
    }
}
