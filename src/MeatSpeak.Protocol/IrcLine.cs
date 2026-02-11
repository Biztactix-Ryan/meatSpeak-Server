namespace MeatSpeak.Protocol;

public static class IrcLine
{
    public static bool TryParse(ReadOnlySpan<byte> line, out IrcLineParts parts)
    {
        parts = default;

        // Strip trailing \r\n
        while (line.Length > 0 && (line[^1] == IrcConstants.CR || line[^1] == IrcConstants.LF))
            line = line[..^1];

        if (line.IsEmpty)
            return false;

        var remaining = line;

        // Parse tags: @tags<space>
        if (remaining[0] == IrcConstants.At)
        {
            remaining = remaining[1..]; // skip @
            int spaceIdx = remaining.IndexOf(IrcConstants.Space);
            if (spaceIdx < 0) return false;
            if (spaceIdx == 0) return false; // Empty tags are invalid
            parts.Tags = remaining[..spaceIdx];
            remaining = remaining[(spaceIdx + 1)..];
            // Skip extra spaces
            while (remaining.Length > 0 && remaining[0] == IrcConstants.Space)
                remaining = remaining[1..];
        }

        // Parse prefix: :prefix<space>
        if (remaining.Length > 0 && remaining[0] == IrcConstants.Colon)
        {
            remaining = remaining[1..]; // skip :
            int spaceIdx = remaining.IndexOf(IrcConstants.Space);
            if (spaceIdx < 0) return false;
            if (spaceIdx == 0) return false; // Empty prefix is invalid
            parts.Prefix = remaining[..spaceIdx];
            remaining = remaining[(spaceIdx + 1)..];
            while (remaining.Length > 0 && remaining[0] == IrcConstants.Space)
                remaining = remaining[1..];
        }

        if (remaining.IsEmpty)
            return false;

        // Parse command
        int cmdEnd = remaining.IndexOf(IrcConstants.Space);
        if (cmdEnd < 0)
        {
            parts.Command = remaining;
            return true; // Command-only message (e.g., QUIT with no params)
        }
        parts.Command = remaining[..cmdEnd];
        remaining = remaining[(cmdEnd + 1)..];
        while (remaining.Length > 0 && remaining[0] == IrcConstants.Space)
            remaining = remaining[1..];

        if (remaining.IsEmpty)
            return true;

        // Parse params: look for :trailing
        // Find " :" sequence that starts trailing (or ":" at start of remaining params)
        int trailingStart = -1;
        if (remaining[0] == IrcConstants.Colon)
        {
            // Trailing starts immediately
            parts.Trailing = remaining[1..];
            parts.HasTrailing = true;
            return true;
        }

        // Search for " :" in remaining
        for (int i = 0; i < remaining.Length - 1; i++)
        {
            if (remaining[i] == IrcConstants.Space && remaining[i + 1] == IrcConstants.Colon)
            {
                trailingStart = i;
                break;
            }
        }

        if (trailingStart >= 0)
        {
            parts.ParamsRaw = remaining[..trailingStart];
            parts.Trailing = remaining[(trailingStart + 2)..]; // skip " :"
            parts.HasTrailing = true;
        }
        else
        {
            parts.ParamsRaw = remaining;
        }

        return true;
    }
}
