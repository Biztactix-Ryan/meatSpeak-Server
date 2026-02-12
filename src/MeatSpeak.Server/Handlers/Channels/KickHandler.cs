namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

[FloodPenalty(2)]
public sealed class KickHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.KICK;
    public SessionState MinimumState => SessionState.Registered;

    public KickHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 2)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.KICK, "Not enough parameters");
            return;
        }

        var channelName = message.GetParam(0)!;
        var targetNick = message.GetParam(1)!;
        var reason = message.GetParam(2) ?? targetNick;

        if (!_server.Channels.TryGetValue(channelName, out var channel))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHCHANNEL,
                channelName, "No such channel");
            return;
        }

        // Check kicker is chanop
        var kickerMembership = channel.GetMember(session.Info.Nickname!);
        if (kickerMembership == null || !kickerMembership.IsOperator)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_CHANOPRIVSNEEDED,
                channelName, "You're not channel operator");
            return;
        }

        // Check target is a member
        if (!channel.IsMember(targetNick))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_USERNOTINCHANNEL,
                targetNick, channelName, "They aren't on that channel");
            return;
        }

        // Broadcast KICK to all channel members
        foreach (var (memberNick, _) in channel.Members)
        {
            var memberSession = _server.FindSessionByNick(memberNick);
            if (memberSession != null)
                await CapHelper.SendWithTimestamp(memberSession, session.Info.Prefix, IrcConstants.KICK, channelName, targetNick, reason);
        }

        // Remove target from channel
        channel.RemoveMember(targetNick);
        var targetSession = _server.FindSessionByNick(targetNick);
        if (targetSession != null)
            targetSession.Info.Channels.Remove(channelName);

        if (channel.Members.Count == 0)
            _server.RemoveChannel(channelName);
    }
}
