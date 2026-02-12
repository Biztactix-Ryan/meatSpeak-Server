namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Data;

public sealed class PartHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly DbWriteQueue? _writeQueue;
    public string Command => IrcConstants.PART;
    public SessionState MinimumState => SessionState.Registered;

    public PartHandler(IServer server, DbWriteQueue? writeQueue = null)
    {
        _server = server;
        _writeQueue = writeQueue;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (await HandlerGuards.CheckNeedMoreParams(session, _server.Config.ServerName, message, 1, IrcConstants.PART))
            return;

        var channelNames = message.GetParam(0)!.Split(',');
        var reason = message.GetParam(1);

        foreach (var rawName in channelNames)
        {
            var name = rawName.Trim();
            if (string.IsNullOrEmpty(name))
                continue;

            var channel = await HandlerGuards.RequireChannel(session, _server.Config.ServerName, _server, name);
            if (channel == null)
                continue;

            if (await HandlerGuards.CheckNotOnChannel(session, _server.Config.ServerName, channel, session.Info.Nickname!))
                continue;

            // Broadcast PART to all channel members (including sender)
            foreach (var (memberNick, _) in channel.Members)
            {
                var memberSession = _server.FindSessionByNick(memberNick);
                if (memberSession != null)
                {
                    if (reason != null)
                        await CapHelper.SendWithTimestamp(memberSession, session.Info.Prefix, IrcConstants.PART, name, reason);
                    else
                        await CapHelper.SendWithTimestamp(memberSession, session.Info.Prefix, IrcConstants.PART, name);
                }
            }

            channel.RemoveMember(session.Info.Nickname!);
            session.Info.Channels.Remove(name);

            var channelRemoved = channel.Members.Count == 0;
            if (channelRemoved)
                _server.RemoveChannel(name);

            _server.Events.Publish(new ChannelPartedEvent(session.Id, session.Info.Nickname!, name, reason));

            // Log PART event for chathistory event-playback
            ChatLogHelper.LogChannelEvent(_writeQueue, name, session.Info.Nickname!, reason ?? string.Empty, IrcConstants.PART);

            // Delete channel from database when it becomes empty
            if (channelRemoved)
                _writeQueue?.TryWrite(new DeleteChannel(name));
        }
    }
}
