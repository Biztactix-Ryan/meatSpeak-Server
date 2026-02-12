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
    /// Builds a composable tag string for a target session, including server-time,
    /// msgid, and any extra tags as appropriate based on the session's caps.
    /// </summary>
    public static string? BuildTags(ISession target, string? msgId = null, string? extraTags = null)
    {
        var parts = new List<string>(3);

        if (HasCap(target, "server-time"))
            parts.Add(TimeTag());

        if (msgId != null && HasCap(target, "message-tags"))
            parts.Add($"msgid={msgId}");

        if (extraTags != null)
            parts.Add(extraTags);

        return parts.Count > 0 ? string.Join(';', parts) : null;
    }

    /// <summary>
    /// Sends a message to a session with composable tags (server-time, msgid, extras).
    /// </summary>
    public static ValueTask SendWithTags(ISession target, string? msgId, string? prefix, string command, params string[] parameters)
    {
        var tags = BuildTags(target, msgId);
        if (tags != null)
            return target.SendTaggedMessageAsync(tags, prefix, command, parameters);
        return target.SendMessageAsync(prefix, command, parameters);
    }

    /// <summary>
    /// Sends a message to a session with composable tags plus extra tag content (e.g., client-only tags, batch refs).
    /// </summary>
    public static ValueTask SendWithTagsAndExtra(ISession target, string? msgId, string? extraTags, string? prefix, string command, params string[] parameters)
    {
        var tags = BuildTags(target, msgId, extraTags);
        if (tags != null)
            return target.SendTaggedMessageAsync(tags, prefix, command, parameters);
        return target.SendMessageAsync(prefix, command, parameters);
    }

    /// <summary>
    /// Sends a message to a session with server-time tag if the session has the cap.
    /// Backward-compatible wrapper around SendWithTags.
    /// </summary>
    public static ValueTask SendWithTimestamp(ISession target, string? prefix, string command, params string[] parameters)
        => SendWithTags(target, null, prefix, command, parameters);

    /// <summary>
    /// Extracts client-only tags (prefixed with +) from an incoming message's tag string.
    /// Returns them as a semicolon-separated string, or null if none.
    /// </summary>
    public static string? ExtractClientTags(string? tagsString)
    {
        if (string.IsNullOrEmpty(tagsString))
            return null;

        var clientTags = new List<string>();
        foreach (var tag in tagsString.Split(';'))
        {
            if (tag.Length > 0 && tag[0] == '+')
                clientTags.Add(tag);
        }

        return clientTags.Count > 0 ? string.Join(';', clientTags) : null;
    }
}
