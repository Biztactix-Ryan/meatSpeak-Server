namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Registration;

[FloodPenalty(2)]
public sealed class NickHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly RegistrationPipeline _registration;
    public string Command => IrcConstants.NICK;
    public SessionState MinimumState => SessionState.Connecting;

    public NickHandler(IServer server, RegistrationPipeline registration)
    {
        _server = server;
        _registration = registration;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 1)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NONICKNAMEGIVEN,
                "No nickname given");
            return;
        }

        var newNick = message.GetParam(0)!;

        // Validate nickname (alphanumeric, _, -, [], {}, \, ^, `)
        if (!IsValidNick(newNick))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_ERRONEUSNICKNAME,
                newNick, "Erroneous nickname");
            return;
        }

        var oldNick = session.Info.Nickname;

        // Atomic nick claim: if the nick is already taken by someone else, fail
        if (!string.Equals(oldNick, newNick, StringComparison.OrdinalIgnoreCase))
        {
            if (!_server.TryClaimNick(newNick, session))
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NICKNAMEINUSE,
                    newNick, "Nickname is already in use");
                return;
            }
            // Remove old nick from index after successfully claiming new one
            if (oldNick != null)
                _server.UpdateNickIndex(oldNick, null, session);
        }

        session.Info.Nickname = newNick;

        if (session.State >= SessionState.Registered && oldNick != null)
        {
            // Record WHOWAS entry for the old nickname
            _server.RecordWhowas(new WhowasEntry(
                oldNick,
                session.Info.Username ?? "~user",
                session.Info.Hostname ?? "unknown",
                session.Info.Realname ?? oldNick,
                DateTimeOffset.UtcNow));

            var prefix = $"{oldNick}!{session.Info.Username}@{session.Info.Hostname}";

            // Broadcast to all users sharing a channel (deduplicated), plus the user themselves
            var notified = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { session.Id };
            await CapHelper.SendWithTimestamp(session, prefix, IrcConstants.NICK, newNick);

            foreach (var channelName in session.Info.Channels)
            {
                if (_server.Channels.TryGetValue(channelName, out var channel))
                {
                    // Atomically update channel membership key
                    channel.UpdateMemberNick(oldNick, newNick);

                    foreach (var (memberNick, _) in channel.Members)
                    {
                        var memberSession = _server.FindSessionByNick(memberNick);
                        if (memberSession != null && notified.Add(memberSession.Id))
                            await CapHelper.SendWithTimestamp(memberSession, prefix, IrcConstants.NICK, newNick);
                    }
                }
            }

            _server.Events.Publish(new NickChangedEvent(session.Id, oldNick, newNick));

            // MONITOR: notify watchers of old nick going offline, new nick coming online
            await MonitorHandler.NotifyOffline(_server, oldNick);
            await MonitorHandler.NotifyOnline(_server, session);
        }

        if (session.State < SessionState.Registered)
        {
            session.State = session.State < SessionState.Registering ? SessionState.Registering : session.State;
            await _registration.TryCompleteRegistrationAsync(session);
        }
    }

    private static bool IsValidNick(string nick)
    {
        if (string.IsNullOrEmpty(nick) || nick.Length > 32)
            return false;
        if (char.IsDigit(nick[0]) || nick[0] == '-')
            return false;
        foreach (var c in nick)
        {
            if (!char.IsLetterOrDigit(c) && c != '_' && c != '-' && c != '[' && c != ']' &&
                c != '{' && c != '}' && c != '\\' && c != '^' && c != '`')
                return false;
        }
        return true;
    }
}
