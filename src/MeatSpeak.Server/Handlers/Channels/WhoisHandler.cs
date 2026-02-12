namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

[FloodPenalty(3)]
public sealed class WhoisHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.WHOIS;
    public SessionState MinimumState => SessionState.Registered;

    public WhoisHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 1)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NONICKNAMEGIVEN,
                "No nickname given");
            return;
        }

        // If two params, the first is the server to query - we ignore it and use the second as nick
        var targetNick = message.Parameters.Count >= 2 ? message.GetParam(1)! : message.GetParam(0)!;

        var targetSession = _server.FindSessionByNick(targetNick);
        if (targetSession == null)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHNICK,
                targetNick, "No such nick/channel");
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_ENDOFWHOIS,
                targetNick, "End of /WHOIS list");
            return;
        }

        var nick = targetSession.Info.Nickname!;

        // RPL_WHOISUSER
        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_WHOISUSER,
            nick,
            targetSession.Info.Username ?? "~user",
            targetSession.Info.Hostname ?? "unknown",
            "*",
            targetSession.Info.Realname ?? nick);

        // RPL_WHOISSERVER
        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_WHOISSERVER,
            nick,
            _server.Config.ServerName,
            _server.Config.Description);

        // RPL_WHOISCHANNELS
        var channels = targetSession.Info.Channels
            .Select(cn => _server.Channels.TryGetValue(cn, out var ch) ? (cn, ch) : default)
            .Where(x => x.ch != null && (!x.ch.Modes.Contains('s') || x.ch.IsMember(session.Info.Nickname!)))
            .Select(x => $"{x.ch!.GetMember(nick)?.PrefixChar ?? ""}{x.cn}")
            .ToList();
        if (channels.Count > 0)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_WHOISCHANNELS,
                nick, string.Join(" ", channels));
        }

        // RPL_WHOISOPERATOR
        if (targetSession.Info.UserModes.Contains('o'))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_WHOISOPERATOR,
                nick, "is an IRC operator");
        }

        // RPL_WHOISIDLE
        var idleSeconds = (long)(DateTimeOffset.UtcNow - targetSession.Info.LastActivity).TotalSeconds;
        var signonUnix = targetSession.Info.ConnectedAt.ToUnixTimeSeconds();
        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_WHOISIDLE,
            nick, idleSeconds.ToString(), signonUnix.ToString(), "seconds idle, signon time");

        // RPL_AWAY
        if (targetSession.Info.AwayMessage != null)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_AWAY,
                nick, targetSession.Info.AwayMessage);
        }

        // RPL_ENDOFWHOIS
        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_ENDOFWHOIS,
            nick, "End of /WHOIS list");
    }
}
