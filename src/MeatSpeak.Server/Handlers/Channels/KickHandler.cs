namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Data;

[FloodPenalty(2)]
public sealed class KickHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly DbWriteQueue? _writeQueue;
    public string Command => IrcConstants.KICK;
    public SessionState MinimumState => SessionState.Registered;

    public KickHandler(IServer server, DbWriteQueue? writeQueue = null)
    {
        _server = server;
        _writeQueue = writeQueue;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (await HandlerGuards.CheckNeedMoreParams(session, _server.Config.ServerName, message, 2, IrcConstants.KICK))
            return;

        var channelName = message.GetParam(0)!;
        var targetNick = message.GetParam(1)!;
        var reason = message.GetParam(2) ?? targetNick;

        var channel = await HandlerGuards.RequireChannel(session, _server.Config.ServerName, _server, channelName);
        if (channel == null)
            return;

        if (await HandlerGuards.CheckChanOpPrivsNeeded(session, _server.Config.ServerName, channel, session.Info.Nickname!))
            return;

        // Check target is a member
        if (!channel.IsMember(targetNick))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_USERNOTINCHANNEL,
                targetNick, channelName, "They aren't on that channel");
            return;
        }

        // Broadcast KICK to all channel members
        await ChannelBroadcaster.BroadcastToChannel(_server, channel, session.Info.Prefix, IrcConstants.KICK, channelName, targetNick, reason);

        // Remove target from channel
        channel.RemoveMember(targetNick);
        var targetSession = _server.FindSessionByNick(targetNick);
        if (targetSession != null)
            targetSession.Info.Channels.Remove(channelName);

        // Log KICK event for chathistory event-playback
        ChatLogHelper.LogChannelEvent(_writeQueue, channelName, session.Info.Nickname!, $"{targetNick} {reason}", IrcConstants.KICK);

        if (channel.Members.Count == 0)
            _server.RemoveChannel(channelName);
    }
}
