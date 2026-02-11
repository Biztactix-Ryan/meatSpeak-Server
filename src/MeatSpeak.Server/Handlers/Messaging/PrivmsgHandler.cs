namespace MeatSpeak.Server.Handlers.Messaging;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

public sealed class PrivmsgHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly IServiceScopeFactory? _scopeFactory;
    public string Command => IrcConstants.PRIVMSG;
    public SessionState MinimumState => SessionState.Registered;

    public PrivmsgHandler(IServer server, IServiceScopeFactory? scopeFactory = null)
    {
        _server = server;
        _scopeFactory = scopeFactory;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 2)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.PRIVMSG, "Not enough parameters");
            return;
        }

        var target = message.GetParam(0)!;
        var text = message.GetParam(1)!;

        if (target.StartsWith('#'))
        {
            // Channel message
            if (!_server.Channels.TryGetValue(target, out var channel))
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHCHANNEL,
                    target, "No such channel");
                return;
            }
            if (!channel.IsMember(session.Info.Nickname!))
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_CANNOTSENDTOCHAN,
                    target, "Cannot send to channel");
                return;
            }

            // Send to all channel members except sender
            foreach (var (nick, _) in channel.Members)
            {
                if (string.Equals(nick, session.Info.Nickname, StringComparison.OrdinalIgnoreCase))
                    continue;
                var targetSession = _server.FindSessionByNick(nick);
                if (targetSession != null)
                    await targetSession.SendMessageAsync(session.Info.Prefix, IrcConstants.PRIVMSG, target, text);
            }
            _server.Events.Publish(new ChannelMessageEvent(session.Id, session.Info.Nickname!, target, text));

            await LogMessageAsync(session.Info.Nickname!, target, null, text, "PRIVMSG", ct);
        }
        else
        {
            // Private message
            var targetSession = _server.FindSessionByNick(target);
            if (targetSession == null)
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHNICK,
                    target, "No such nick/channel");
                return;
            }
            await targetSession.SendMessageAsync(session.Info.Prefix, IrcConstants.PRIVMSG, target, text);
            _server.Events.Publish(new PrivateMessageEvent(session.Id, session.Info.Nickname!, target, text));

            await LogMessageAsync(session.Info.Nickname!, null, target, text, "PRIVMSG", ct);
        }
    }

    private async ValueTask LogMessageAsync(string sender, string? channel, string? target, string text, string type, CancellationToken ct)
    {
        if (_scopeFactory == null) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var chatLogs = scope.ServiceProvider.GetRequiredService<IChatLogRepository>();
            await chatLogs.AddAsync(new ChatLogEntity
            {
                ChannelName = channel,
                Target = target,
                Sender = sender,
                Message = text,
                MessageType = type,
                SentAt = DateTimeOffset.UtcNow,
            }, ct);
        }
        catch { /* DB logging failure should not break messaging */ }
    }
}
