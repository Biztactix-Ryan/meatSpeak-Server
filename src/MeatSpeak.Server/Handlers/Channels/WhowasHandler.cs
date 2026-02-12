namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

[FloodPenalty(3)]
public sealed class WhowasHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.WHOWAS;
    public SessionState MinimumState => SessionState.Registered;

    public WhowasHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 1)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NONICKNAMEGIVEN,
                "No nickname given");
            return;
        }

        var targetNick = message.GetParam(0)!;
        var maxCount = 10;
        if (message.Parameters.Count >= 2 && int.TryParse(message.GetParam(1), out var count) && count > 0)
            maxCount = count;

        var entries = _server.GetWhowas(targetNick, maxCount);

        if (entries.Count == 0)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_WASNOSUCHNICK,
                targetNick, "There was no such nickname");
        }
        else
        {
            foreach (var entry in entries)
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_WHOWASUSER,
                    entry.Nickname,
                    entry.Username,
                    entry.Hostname,
                    "*",
                    entry.Realname);

                await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_WHOISSERVER,
                    entry.Nickname,
                    _server.Config.ServerName,
                    _server.Config.Description);
            }
        }

        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_ENDOFWHOWAS,
            targetNick, "End of WHOWAS");
    }
}
