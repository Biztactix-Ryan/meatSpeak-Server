namespace MeatSpeak.Server.Handlers.Messaging;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

[FloodPenalty(2)]
public sealed class RedactHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly DbWriteQueue? _writeQueue;
    private readonly IServiceScopeFactory? _scopeFactory;
    public string Command => IrcConstants.REDACT;
    public SessionState MinimumState => SessionState.Registered;

    public RedactHandler(IServer server, DbWriteQueue? writeQueue = null, IServiceScopeFactory? scopeFactory = null)
    {
        _server = server;
        _writeQueue = writeQueue;
        _scopeFactory = scopeFactory;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        // Sender must have negotiated draft/message-redaction
        if (!CapHelper.HasCap(session, "draft/message-redaction"))
        {
            await session.SendMessageAsync(_server.Config.ServerName, "FAIL",
                IrcConstants.REDACT, "NEED_CAPABILITY", "draft/message-redaction capability required");
            return;
        }

        // REDACT <target> <msgid> [reason]
        if (message.Parameters.Count < 2)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.REDACT, "Not enough parameters");
            return;
        }

        var target = message.GetParam(0)!;
        var msgId = message.GetParam(1)!;
        var reason = message.GetParam(2);

        // Look up the message to validate permissions
        if (_scopeFactory != null)
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IChatLogRepository>();
            var originalMsg = await repo.GetByMsgIdAsync(msgId, ct);

            if (originalMsg == null)
            {
                await SendFail(session, target, msgId, "UNKNOWN_MSGID", "This message does not exist or is too old");
                return;
            }

            // Check permission: sender can redact their own messages
            var nick = session.Info.Nickname!;
            var isSender = string.Equals(originalMsg.Sender, nick, StringComparison.OrdinalIgnoreCase);

            if (!isSender)
            {
                // For channel messages, channel operators can redact
                if (target.StartsWith('#'))
                {
                    if (_server.Channels.TryGetValue(target, out var ch))
                    {
                        var membership = ch.GetMember(nick);
                        if (membership == null || !membership.IsOperator)
                        {
                            await SendFail(session, target, msgId, "REDACT_FORBIDDEN", "You are not authorised to delete this message");
                            return;
                        }
                    }
                }
                else
                {
                    // For PMs, only the sender can redact
                    await SendFail(session, target, msgId, "REDACT_FORBIDDEN", "You are not authorised to delete this message");
                    return;
                }
            }
        }

        if (target.StartsWith('#'))
        {
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

            // Forward REDACT to all channel members with draft/message-redaction cap
            foreach (var (nick, _) in channel.Members)
            {
                var targetSession = _server.FindSessionByNick(nick);
                if (targetSession != null && CapHelper.HasCap(targetSession, "draft/message-redaction"))
                {
                    if (reason != null)
                        await CapHelper.SendWithTags(targetSession, null, session.Info.Prefix, IrcConstants.REDACT, target, msgId, reason);
                    else
                        await CapHelper.SendWithTags(targetSession, null, session.Info.Prefix, IrcConstants.REDACT, target, msgId);
                }
            }
        }
        else
        {
            var targetSession = _server.FindSessionByNick(target);
            if (targetSession == null)
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHNICK,
                    target, "No such nick/channel");
                return;
            }

            if (CapHelper.HasCap(targetSession, "draft/message-redaction"))
            {
                if (reason != null)
                    await CapHelper.SendWithTags(targetSession, null, session.Info.Prefix, IrcConstants.REDACT, target, msgId, reason);
                else
                    await CapHelper.SendWithTags(targetSession, null, session.Info.Prefix, IrcConstants.REDACT, target, msgId);
            }

            // Echo to sender
            if (CapHelper.HasCap(session, "draft/message-redaction"))
            {
                if (reason != null)
                    await CapHelper.SendWithTags(session, null, session.Info.Prefix, IrcConstants.REDACT, target, msgId, reason);
                else
                    await CapHelper.SendWithTags(session, null, session.Info.Prefix, IrcConstants.REDACT, target, msgId);
            }
        }

        // Queue redaction to DB
        _writeQueue?.TryWrite(new RedactChatLog(msgId, session.Info.Nickname!));
    }

    private ValueTask SendFail(ISession session, string target, string msgId, string code, string description)
        => session.SendMessageAsync(_server.Config.ServerName, "FAIL", IrcConstants.REDACT, code, target, msgId, description);
}
