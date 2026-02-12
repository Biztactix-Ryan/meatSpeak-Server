namespace MeatSpeak.Server.Handlers.Messaging;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Capabilities;

[FloodPenalty(2)]
public sealed class TagmsgHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.TAGMSG;
    public SessionState MinimumState => SessionState.Registered;

    public TagmsgHandler(IServer server)
    {
        _server = server;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (!CapHelper.HasCap(session, "message-tags"))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_UNKNOWNCOMMAND,
                IrcConstants.TAGMSG, "You must negotiate message-tags capability first");
            return;
        }

        if (message.Parameters.Count < 1)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NORECIPIENT,
                "No recipient given (TAGMSG)");
            return;
        }

        var target = message.GetParam(0)!;
        var msgId = MsgIdGenerator.Generate();
        var clientTags = CapHelper.ExtractClientTags(message.Tags);

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

            foreach (var (nick, _) in channel.Members)
            {
                if (string.Equals(nick, session.Info.Nickname, StringComparison.OrdinalIgnoreCase))
                    continue;
                var targetSession = _server.FindSessionByNick(nick);
                if (targetSession != null && CapHelper.HasCap(targetSession, "message-tags"))
                    await CapHelper.SendWithTagsAndExtra(targetSession, msgId, clientTags, session.Info.Prefix, IrcConstants.TAGMSG, target);
            }

            // echo-message
            if (CapHelper.HasCap(session, "echo-message"))
                await CapHelper.SendWithTagsAndExtra(session, msgId, clientTags, session.Info.Prefix, IrcConstants.TAGMSG, target);
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

            if (CapHelper.HasCap(targetSession, "message-tags"))
                await CapHelper.SendWithTagsAndExtra(targetSession, msgId, clientTags, session.Info.Prefix, IrcConstants.TAGMSG, target);

            // echo-message
            if (CapHelper.HasCap(session, "echo-message"))
                await CapHelper.SendWithTagsAndExtra(session, msgId, clientTags, session.Info.Prefix, IrcConstants.TAGMSG, target);
        }
    }
}
