namespace MeatSpeak.Server.Handlers.Messaging;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class NoticeHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.NOTICE;
    public SessionState MinimumState => SessionState.Registered;

    public NoticeHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 2) return; // NOTICE silently fails per RFC

        var target = message.GetParam(0)!;
        var text = message.GetParam(1)!;

        if (target.StartsWith('#'))
        {
            if (!_server.Channels.TryGetValue(target, out var channel)) return;
            if (!channel.IsMember(session.Info.Nickname!)) return;

            foreach (var (nick, _) in channel.Members)
            {
                if (string.Equals(nick, session.Info.Nickname, StringComparison.OrdinalIgnoreCase))
                    continue;
                var targetSession = _server.FindSessionByNick(nick);
                if (targetSession != null)
                    await targetSession.SendMessageAsync(session.Info.Prefix, IrcConstants.NOTICE, target, text);
            }
        }
        else
        {
            var targetSession = _server.FindSessionByNick(target);
            if (targetSession != null)
                await targetSession.SendMessageAsync(session.Info.Prefix, IrcConstants.NOTICE, target, text);
        }
    }
}
