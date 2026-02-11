namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

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
        var channels = new List<string>();
        foreach (var channelName in targetSession.Info.Channels)
        {
            if (_server.Channels.TryGetValue(channelName, out var channel))
            {
                // Skip secret channels unless the querier is also a member
                if (channel.Modes.Contains('s') && !channel.IsMember(session.Info.Nickname!))
                    continue;

                var membership = channel.GetMember(nick);
                var prefix = membership?.PrefixChar ?? "";
                channels.Add($"{prefix}{channelName}");
            }
        }
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

        // RPL_ENDOFWHOIS
        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_ENDOFWHOIS,
            nick, "End of /WHOIS list");
    }
}
