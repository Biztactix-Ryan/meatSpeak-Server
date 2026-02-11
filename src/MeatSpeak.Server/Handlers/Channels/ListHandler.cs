namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class ListHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.LIST;
    public SessionState MinimumState => SessionState.Registered;

    public ListHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        foreach (var channel in _server.Channels.Values)
        {
            // Skip secret channels unless the user is a member
            if (channel.Modes.Contains('s') && !channel.IsMember(session.Info.Nickname!))
                continue;

            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_LIST,
                channel.Name, channel.Members.Count.ToString(), channel.Topic ?? "");
        }

        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_LISTEND,
            "End of /LIST");
    }
}
