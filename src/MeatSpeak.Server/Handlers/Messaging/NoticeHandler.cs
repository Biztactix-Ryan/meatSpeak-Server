namespace MeatSpeak.Server.Handlers.Messaging;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Diagnostics;

[FloodPenalty(2)]
public sealed class NoticeHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly DbWriteQueue? _writeQueue;
    private readonly ServerMetrics? _metrics;
    public string Command => IrcConstants.NOTICE;
    public SessionState MinimumState => SessionState.Registered;

    public NoticeHandler(IServer server, DbWriteQueue? writeQueue = null, ServerMetrics? metrics = null)
    {
        _server = server;
        _writeQueue = writeQueue;
        _metrics = metrics;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 2) return; // NOTICE silently fails per RFC

        var target = message.GetParam(0)!;
        var text = message.GetParam(1)!;
        var msgId = MsgIdGenerator.Generate();
        var clientTags = CapHelper.ExtractClientTags(message.Tags);

        if (target.StartsWith('#'))
        {
            if (!_server.Channels.TryGetValue(target, out var channel)) return;
            if (!channel.IsMember(session.Info.Nickname!)) return;

            // Enforce moderated mode (+m): silently drop for non-ops/non-voiced (NOTICE per RFC)
            if (channel.Modes.Contains('m'))
            {
                var member = channel.GetMember(session.Info.Nickname!);
                if (member is not { IsOperator: true } and not { HasVoice: true })
                    return;
            }

            var broadcastStart = ServerMetrics.GetTimestamp();
            foreach (var (nick, _) in channel.Members)
            {
                if (string.Equals(nick, session.Info.Nickname, StringComparison.OrdinalIgnoreCase))
                    continue;
                var targetSession = _server.FindSessionByNick(nick);
                if (targetSession != null)
                {
                    var extra = CapHelper.HasCap(targetSession, "message-tags") ? clientTags : null;
                    await CapHelper.SendWithTagsAndExtra(targetSession, msgId, extra, session.Info.Prefix, IrcConstants.NOTICE, target, text);
                }
            }
            _metrics?.RecordBroadcastDuration(ServerMetrics.GetElapsedMs(broadcastStart));
            _metrics?.MessageBroadcast();

            if (CapHelper.HasCap(session, "echo-message"))
            {
                var extra = CapHelper.HasCap(session, "message-tags") ? clientTags : null;
                await CapHelper.SendWithTagsAndExtra(session, msgId, extra, session.Info.Prefix, IrcConstants.NOTICE, target, text);
            }

            ChatLogHelper.LogMessage(_writeQueue, session.Info.Nickname!, target, null, text, "NOTICE", msgId);
        }
        else
        {
            var targetSession = _server.FindSessionByNick(target);
            if (targetSession != null)
            {
                var extra = CapHelper.HasCap(targetSession, "message-tags") ? clientTags : null;
                await CapHelper.SendWithTagsAndExtra(targetSession, msgId, extra, session.Info.Prefix, IrcConstants.NOTICE, target, text);
            }

            if (CapHelper.HasCap(session, "echo-message"))
            {
                var extra = CapHelper.HasCap(session, "message-tags") ? clientTags : null;
                await CapHelper.SendWithTagsAndExtra(session, msgId, extra, session.Info.Prefix, IrcConstants.NOTICE, target, text);
            }

            _metrics?.MessagePrivate();

            ChatLogHelper.LogMessage(_writeQueue, session.Info.Nickname!, null, target, text, "NOTICE", msgId);
        }
    }
}
