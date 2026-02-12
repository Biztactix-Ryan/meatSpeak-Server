namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class AwayHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.AWAY;
    public SessionState MinimumState => SessionState.Registered;

    public AwayHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        var awayMessage = message.GetParam(0);

        if (string.IsNullOrEmpty(awayMessage))
        {
            // Unset away
            session.Info.AwayMessage = null;
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_UNAWAY,
                "You are no longer marked as being away");

            // away-notify: broadcast to shared channel members
            await BroadcastAwayNotify(session);
        }
        else
        {
            // Set away
            session.Info.AwayMessage = awayMessage;
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_NOWAWAY,
                "You have been marked as being away");

            // away-notify: broadcast to shared channel members
            await BroadcastAwayNotify(session, awayMessage);
        }
    }

    private async ValueTask BroadcastAwayNotify(ISession session, string? awayMessage = null)
    {
        var notified = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { session.Id };

        foreach (var channelName in session.Info.Channels)
        {
            if (!_server.Channels.TryGetValue(channelName, out var channel))
                continue;

            foreach (var (memberNick, _) in channel.Members)
            {
                var memberSession = _server.FindSessionByNick(memberNick);
                if (memberSession != null && notified.Add(memberSession.Id) && CapHelper.HasCap(memberSession, "away-notify"))
                {
                    if (awayMessage != null)
                        await CapHelper.SendWithTimestamp(memberSession, session.Info.Prefix, IrcConstants.AWAY, awayMessage);
                    else
                        await CapHelper.SendWithTimestamp(memberSession, session.Info.Prefix, IrcConstants.AWAY);
                }
            }
        }
    }
}
