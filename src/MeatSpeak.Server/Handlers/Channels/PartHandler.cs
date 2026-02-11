namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

public sealed class PartHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly IServiceScopeFactory? _scopeFactory;
    public string Command => IrcConstants.PART;
    public SessionState MinimumState => SessionState.Registered;

    public PartHandler(IServer server, IServiceScopeFactory? scopeFactory = null)
    {
        _server = server;
        _scopeFactory = scopeFactory;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 1)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.PART, "Not enough parameters");
            return;
        }

        var channelNames = message.GetParam(0)!.Split(',');
        var reason = message.GetParam(1);

        foreach (var rawName in channelNames)
        {
            var name = rawName.Trim();
            if (string.IsNullOrEmpty(name))
                continue;

            if (!_server.Channels.TryGetValue(name, out var channel))
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHCHANNEL,
                    name, "No such channel");
                continue;
            }

            if (!channel.IsMember(session.Info.Nickname!))
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOTONCHANNEL,
                    name, "You're not on that channel");
                continue;
            }

            // Broadcast PART to all channel members (including sender)
            foreach (var (memberNick, _) in channel.Members)
            {
                var memberSession = _server.FindSessionByNick(memberNick);
                if (memberSession != null)
                {
                    if (reason != null)
                        await memberSession.SendMessageAsync(session.Info.Prefix, IrcConstants.PART, name, reason);
                    else
                        await memberSession.SendMessageAsync(session.Info.Prefix, IrcConstants.PART, name);
                }
            }

            channel.RemoveMember(session.Info.Nickname!);
            session.Info.Channels.Remove(name);

            var channelRemoved = channel.Members.Count == 0;
            if (channelRemoved)
                _server.RemoveChannel(name);

            _server.Events.Publish(new ChannelPartedEvent(session.Id, session.Info.Nickname!, name, reason));

            // Delete channel from database when it becomes empty
            if (channelRemoved)
                await DeleteChannelAsync(name);
        }
    }

    private async ValueTask DeleteChannelAsync(string name)
    {
        if (_scopeFactory == null) return;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var channels = scope.ServiceProvider.GetRequiredService<IChannelRepository>();
            await channels.DeleteAsync(name);
        }
        catch { /* DB persistence failure should not break channel operations */ }
    }
}
