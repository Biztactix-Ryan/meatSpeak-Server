namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Registration;

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

        // Check for collision
        var existing = _server.FindSessionByNick(newNick);
        if (existing != null && existing.Id != session.Id)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NICKNAMEINUSE,
                newNick, "Nickname is already in use");
            return;
        }

        var oldNick = session.Info.Nickname;
        session.Info.Nickname = newNick;
        _server.UpdateNickIndex(oldNick, newNick, session);

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
            await session.SendMessageAsync(prefix, IrcConstants.NICK, newNick);

            foreach (var channelName in session.Info.Channels)
            {
                if (_server.Channels.TryGetValue(channelName, out var channel))
                {
                    // Update channel membership key: remove old nick, re-add with new nick
                    var membership = channel.GetMember(oldNick);
                    if (membership != null)
                    {
                        channel.RemoveMember(oldNick);
                        membership.Nickname = newNick;
                        channel.AddMember(newNick, membership);
                    }

                    foreach (var (memberNick, _) in channel.Members)
                    {
                        var memberSession = _server.FindSessionByNick(memberNick);
                        if (memberSession != null && notified.Add(memberSession.Id))
                            await memberSession.SendMessageAsync(prefix, IrcConstants.NICK, newNick);
                    }
                }
            }

            _server.Events.Publish(new NickChangedEvent(session.Id, oldNick, newNick));
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
