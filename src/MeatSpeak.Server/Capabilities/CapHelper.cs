namespace MeatSpeak.Server.Capabilities;

using MeatSpeak.Server.Core.Sessions;

public static class CapHelper
{
    public static bool HasCap(ISession session, string cap)
        => session.Info.CapState.Acknowledged.Contains(cap);

    /// <summary>
    /// Builds the server-time tag value in ISO 8601 UTC format.
    /// </summary>
    public static string TimeTag()
        => $"time={DateTimeOffset.UtcNow:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}";

    /// <summary>
    /// Sends a message to a session with server-time tag if the session has the cap.
    /// </summary>
    public static ValueTask SendWithTimestamp(ISession target, string? prefix, string command, params string[] parameters)
    {
        if (HasCap(target, "server-time"))
            return target.SendTaggedMessageAsync(TimeTag(), prefix, command, parameters);
        return target.SendMessageAsync(prefix, command, parameters);
    }
}
