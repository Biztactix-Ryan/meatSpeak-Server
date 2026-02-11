namespace MeatSpeak.Server.Handlers.Operator;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class KillHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.KILL;
    public SessionState MinimumState => SessionState.Registered;

    public KillHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 2)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.KILL, "Not enough parameters");
            return;
        }

        // Require IRC operator
        if (!session.Info.UserModes.Contains('o'))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOPRIVILEGES,
                "Permission Denied- You're not an IRC operator");
            return;
        }

        var targetNick = message.GetParam(0)!;
        var reason = message.GetParam(1)!;

        var targetSession = _server.FindSessionByNick(targetNick);
        if (targetSession == null)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHNICK,
                targetNick, "No such nick/channel");
            return;
        }

        var killReason = $"Killed ({session.Info.Nickname} ({reason}))";

        // Broadcast QUIT to all channels the target was in
        var notified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var channelName in targetSession.Info.Channels.ToList())
        {
            if (_server.Channels.TryGetValue(channelName, out var channel))
            {
                foreach (var (memberNick, _) in channel.Members)
                {
                    if (string.Equals(memberNick, targetNick, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (notified.Add(memberNick))
                    {
                        var memberSession = _server.FindSessionByNick(memberNick);
                        if (memberSession != null)
                            await memberSession.SendMessageAsync(targetSession.Info.Prefix, IrcConstants.QUIT, killReason);
                    }
                }
            }
        }

        await targetSession.DisconnectAsync(killReason);
    }
}
