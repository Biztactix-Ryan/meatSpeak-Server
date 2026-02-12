namespace MeatSpeak.Server.Handlers;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

/// <summary>
/// Shared guard checks for IRC command handlers.
/// Methods that return bool return true if the check FAILED (caller should return).
/// RequireChannel returns null on failure (caller should return/continue).
/// </summary>
internal static class HandlerGuards
{
    /// <summary>
    /// Sends ERR_NEEDMOREPARAMS if parameter count is insufficient. Returns true if check failed.
    /// </summary>
    public static async ValueTask<bool> CheckNeedMoreParams(
        ISession session, string serverName, IrcMessage message,
        int minCount, string command)
    {
        if (message.Parameters.Count >= minCount)
            return false;

        await session.SendNumericAsync(serverName, Numerics.ERR_NEEDMOREPARAMS,
            command, "Not enough parameters");
        return true;
    }

    /// <summary>
    /// Looks up a channel by name, sends ERR_NOSUCHCHANNEL if not found. Returns null on failure.
    /// </summary>
    public static async ValueTask<IChannel?> RequireChannel(
        ISession session, string serverName, IServer server,
        string channelName)
    {
        if (server.Channels.TryGetValue(channelName, out var channel))
            return channel;

        await session.SendNumericAsync(serverName, Numerics.ERR_NOSUCHCHANNEL,
            channelName, "No such channel");
        return null;
    }

    /// <summary>
    /// Checks that the session's nick is a member of the channel.
    /// Sends ERR_NOTONCHANNEL if not. Returns true if check failed.
    /// </summary>
    public static async ValueTask<bool> CheckNotOnChannel(
        ISession session, string serverName,
        IChannel channel, string nickname)
    {
        if (channel.IsMember(nickname))
            return false;

        await session.SendNumericAsync(serverName, Numerics.ERR_NOTONCHANNEL,
            channel.Name, "You're not on that channel");
        return true;
    }

    /// <summary>
    /// Checks that the session's nick is a channel operator.
    /// Sends ERR_CHANOPRIVSNEEDED if not. Returns true if check failed.
    /// </summary>
    public static async ValueTask<bool> CheckChanOpPrivsNeeded(
        ISession session, string serverName,
        IChannel channel, string nickname)
    {
        var membership = channel.GetMember(nickname);
        if (membership is { IsOperator: true })
            return false;

        await session.SendNumericAsync(serverName, Numerics.ERR_CHANOPRIVSNEEDED,
            channel.Name, "You're not channel operator");
        return true;
    }
}
