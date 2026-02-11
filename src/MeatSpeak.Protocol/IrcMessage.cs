namespace MeatSpeak.Protocol;

public sealed class IrcMessage
{
    public string? Tags { get; }
    public string? Prefix { get; }
    public string Command { get; }
    public IReadOnlyList<string> Parameters { get; }

    public IrcMessage(string? tags, string? prefix, string command, IReadOnlyList<string> parameters)
    {
        Tags = tags;
        Prefix = prefix;
        Command = command;
        Parameters = parameters;
    }

    // Convenience: get the trailing parameter (last parameter)
    public string? Trailing => Parameters.Count > 0 ? Parameters[^1] : null;

    // Convenience: get parameter at index, or null
    public string? GetParam(int index) => index < Parameters.Count ? Parameters[index] : null;

    // Parse tags lazily
    public Dictionary<string, string?> ParsedTags => IrcTags.Parse(Tags);

    // Parse prefix into nick!user@host
    public (string? Nick, string? User, string? Host) ParsePrefix()
    {
        if (Prefix == null) return (null, null, null);

        int excl = Prefix.IndexOf('!');
        int at = Prefix.IndexOf('@');

        if (excl >= 0 && at > excl)
            return (Prefix[..excl], Prefix[(excl + 1)..at], Prefix[(at + 1)..]);
        if (at >= 0)
            return (Prefix[..at], null, Prefix[(at + 1)..]);
        return (Prefix, null, null);
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        if (Tags != null) sb.Append('@').Append(Tags).Append(' ');
        if (Prefix != null) sb.Append(':').Append(Prefix).Append(' ');
        sb.Append(Command);
        if (Parameters.Count > 0)
        {
            for (int i = 0; i < Parameters.Count - 1; i++)
                sb.Append(' ').Append(Parameters[i]);
            // Last param gets : prefix if it contains spaces or is trailing
            var last = Parameters[^1];
            if (Parameters.Count == 1 && !last.Contains(' '))
                sb.Append(' ').Append(last);
            else
                sb.Append(" :").Append(last);
        }
        return sb.ToString();
    }
}
