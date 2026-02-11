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

        if (session.State >= SessionState.Registered && oldNick != null)
        {
            // Notify the user and all shared channels
            var prefix = $"{oldNick}!{session.Info.Username}@{session.Info.Hostname}";
            await session.SendMessageAsync(prefix, IrcConstants.NICK, newNick);
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
