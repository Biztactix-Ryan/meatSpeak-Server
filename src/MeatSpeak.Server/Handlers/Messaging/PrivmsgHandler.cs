namespace MeatSpeak.Server.Handlers.Messaging;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Diagnostics;

[FloodPenalty(2)]
public sealed class PrivmsgHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly DbWriteQueue? _writeQueue;
    private readonly ServerMetrics? _metrics;
    public string Command => IrcConstants.PRIVMSG;
    public SessionState MinimumState => SessionState.Registered;

    public PrivmsgHandler(IServer server, DbWriteQueue? writeQueue = null, ServerMetrics? metrics = null)
    {
        _server = server;
        _writeQueue = writeQueue;
        _metrics = metrics;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 1)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NORECIPIENT,
                "No recipient given (PRIVMSG)");
            return;
        }

        if (message.Parameters.Count < 2)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOTEXTTOSEND,
                "No text to send");
            return;
        }

        var target = message.GetParam(0)!;
        var text = message.GetParam(1)!;
        var msgId = MsgIdGenerator.Generate();
        var clientTags = CapHelper.ExtractClientTags(message.Tags);

        if (target.StartsWith('#'))
        {
            // Channel message
            if (!_server.Channels.TryGetValue(target, out var channel))
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHCHANNEL,
                    target, "No such channel");
                return;
            }
            if (!channel.IsMember(session.Info.Nickname!))
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_CANNOTSENDTOCHAN,
                    target, "Cannot send to channel");
                return;
            }

            // Enforce moderated mode (+m): only ops and voiced users can speak
            if (channel.Modes.Contains('m'))
            {
                var member = channel.GetMember(session.Info.Nickname!);
                if (member is not { IsOperator: true } and not { HasVoice: true })
                {
                    await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_CANNOTSENDTOCHAN,
                        target, "Cannot send to channel");
                    return;
                }
            }

            // Send to all channel members except sender
            var broadcastStart = ServerMetrics.GetTimestamp();
            foreach (var (nick, _) in channel.Members)
            {
                if (string.Equals(nick, session.Info.Nickname, StringComparison.OrdinalIgnoreCase))
                    continue;
                var targetSession = _server.FindSessionByNick(nick);
                if (targetSession != null)
                {
                    var extra = CapHelper.HasCap(targetSession, "message-tags") ? clientTags : null;
                    await CapHelper.SendWithTagsAndExtra(targetSession, msgId, extra, session.Info.Prefix, IrcConstants.PRIVMSG, target, text);
                }
            }
            _metrics?.RecordBroadcastDuration(ServerMetrics.GetElapsedMs(broadcastStart));
            _metrics?.MessageBroadcast();
            _server.Events.Publish(new ChannelMessageEvent(session.Id, session.Info.Nickname!, target, text));

            // echo-message: echo back to sender
            if (CapHelper.HasCap(session, "echo-message"))
            {
                var extra = CapHelper.HasCap(session, "message-tags") ? clientTags : null;
                await CapHelper.SendWithTagsAndExtra(session, msgId, extra, session.Info.Prefix, IrcConstants.PRIVMSG, target, text);
            }

            ChatLogHelper.LogMessage(_writeQueue, session.Info.Nickname!, target, null, text, "PRIVMSG", msgId);
        }
        else
        {
            // Private message
            var targetSession = _server.FindSessionByNick(target);
            if (targetSession == null)
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHNICK,
                    target, "No such nick/channel");
                return;
            }
            {
                var extra = CapHelper.HasCap(targetSession, "message-tags") ? clientTags : null;
                await CapHelper.SendWithTagsAndExtra(targetSession, msgId, extra, session.Info.Prefix, IrcConstants.PRIVMSG, target, text);
            }

            // echo-message: echo back to sender for private messages
            if (CapHelper.HasCap(session, "echo-message"))
            {
                var extra = CapHelper.HasCap(session, "message-tags") ? clientTags : null;
                await CapHelper.SendWithTagsAndExtra(session, msgId, extra, session.Info.Prefix, IrcConstants.PRIVMSG, target, text);
            }

            // RPL_AWAY - notify sender if target is away
            if (targetSession.Info.AwayMessage != null)
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_AWAY,
                    target, targetSession.Info.AwayMessage);
            }

            _metrics?.MessagePrivate();
            _server.Events.Publish(new PrivateMessageEvent(session.Id, session.Info.Nickname!, target, text));

            ChatLogHelper.LogMessage(_writeQueue, session.Info.Nickname!, null, target, text, "PRIVMSG", msgId);
        }
    }
}
