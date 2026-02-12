namespace MeatSpeak.Server.Handlers;

using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Server;

internal static class ChannelBroadcaster
{
    /// <summary>
    /// Broadcasts a message (with server-time tag) to all members of a channel.
    /// </summary>
    public static async ValueTask BroadcastToChannel(
        IServer server, IChannel channel,
        string? prefix, string command, params string[] parameters)
    {
        foreach (var (memberNick, _) in channel.Members)
        {
            var memberSession = server.FindSessionByNick(memberNick);
            if (memberSession != null)
                await CapHelper.SendWithTimestamp(memberSession, prefix, command, parameters);
        }
    }

    /// <summary>
    /// Broadcasts a message across multiple channels, deduplicating by nick.
    /// Returns the set of nicks that were notified.
    /// </summary>
    public static async ValueTask<HashSet<string>> BroadcastAcrossChannels(
        IServer server, IEnumerable<string> channelNames,
        string? skipNick,
        string? prefix, string command, params string[] parameters)
    {
        var notified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var channelName in channelNames)
        {
            if (server.Channels.TryGetValue(channelName, out var channel))
            {
                foreach (var (memberNick, _) in channel.Members)
                {
                    if (skipNick != null &&
                        string.Equals(memberNick, skipNick, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (notified.Add(memberNick))
                    {
                        var memberSession = server.FindSessionByNick(memberNick);
                        if (memberSession != null)
                            await CapHelper.SendWithTimestamp(memberSession, prefix, command, parameters);
                    }
                }
            }
        }
        return notified;
    }
}
