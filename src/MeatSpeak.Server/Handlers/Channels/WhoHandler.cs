namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class WhoHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.WHO;
    public SessionState MinimumState => SessionState.Registered;

    public WhoHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        var target = message.GetParam(0) ?? "*";

        if (target.StartsWith('#'))
        {
            // Channel WHO
            if (_server.Channels.TryGetValue(target, out var channel))
            {
                foreach (var (nick, membership) in channel.Members)
                {
                    var memberSession = _server.FindSessionByNick(nick);
                    if (memberSession == null)
                        continue;

                    var flags = (memberSession.Info.AwayMessage != null ? "G" : "H") + membership.PrefixChar;
                    await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_WHOREPLY,
                        target,
                        memberSession.Info.Username ?? "~user",
                        memberSession.Info.Hostname ?? "unknown",
                        _server.Config.ServerName,
                        nick,
                        flags,
                        $"0 {memberSession.Info.Realname ?? nick}");
                }
            }
        }
        else
        {
            // Nick WHO - find matching session
            var targetSession = _server.FindSessionByNick(target);
            if (targetSession != null)
            {
                var flags = targetSession.Info.AwayMessage != null ? "G" : "H";
                if (targetSession.Info.UserModes.Contains('o'))
                    flags += "*";

                await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_WHOREPLY,
                    "*",
                    targetSession.Info.Username ?? "~user",
                    targetSession.Info.Hostname ?? "unknown",
                    _server.Config.ServerName,
                    targetSession.Info.Nickname!,
                    flags,
                    $"0 {targetSession.Info.Realname ?? targetSession.Info.Nickname}");
            }
        }

        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_ENDOFWHO,
            target, "End of /WHO list");
    }
}
