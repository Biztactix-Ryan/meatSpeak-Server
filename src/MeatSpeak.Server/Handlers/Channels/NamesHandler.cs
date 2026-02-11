namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class NamesHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.NAMES;
    public SessionState MinimumState => SessionState.Registered;

    public NamesHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count >= 1)
        {
            var channelNames = message.GetParam(0)!.Split(',');
            foreach (var rawName in channelNames)
            {
                var name = rawName.Trim();
                if (string.IsNullOrEmpty(name))
                    continue;

                if (_server.Channels.TryGetValue(name, out var channel))
                {
                    await JoinHandler.SendNamesReply(session, channel, _server.Config.ServerName);
                }
                else
                {
                    await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_ENDOFNAMES,
                        name, "End of /NAMES list");
                }
            }
        }
        else
        {
            // No channel specified - list all visible users
            foreach (var channel in _server.Channels.Values)
            {
                if (channel.Modes.Contains('s') && !channel.IsMember(session.Info.Nickname!))
                    continue;
                await JoinHandler.SendNamesReply(session, channel, _server.Config.ServerName);
            }
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_ENDOFNAMES,
                "*", "End of /NAMES list");
        }
    }
}
