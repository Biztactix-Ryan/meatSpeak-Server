namespace MeatSpeak.Server.Handlers.Messaging;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;

public sealed class PrivmsgHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.PRIVMSG;
    public SessionState MinimumState => SessionState.Registered;

    public PrivmsgHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 2)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.PRIVMSG, "Not enough parameters");
            return;
        }

        var target = message.GetParam(0)!;
        var text = message.GetParam(1)!;

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

            // Send to all channel members except sender
            foreach (var (nick, _) in channel.Members)
            {
                if (string.Equals(nick, session.Info.Nickname, StringComparison.OrdinalIgnoreCase))
                    continue;
                var targetSession = _server.FindSessionByNick(nick);
                if (targetSession != null)
                    await targetSession.SendMessageAsync(session.Info.Prefix, IrcConstants.PRIVMSG, target, text);
            }
            _server.Events.Publish(new ChannelMessageEvent(session.Id, session.Info.Nickname!, target, text));
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
            await targetSession.SendMessageAsync(session.Info.Prefix, IrcConstants.PRIVMSG, target, text);
            _server.Events.Publish(new PrivateMessageEvent(session.Id, session.Info.Nickname!, target, text));
        }
    }
}
