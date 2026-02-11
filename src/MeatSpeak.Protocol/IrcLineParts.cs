namespace MeatSpeak.Protocol;

public ref struct IrcLineParts
{
    public ReadOnlySpan<byte> Tags;
    public ReadOnlySpan<byte> Prefix;
    public ReadOnlySpan<byte> Command;
    public ReadOnlySpan<byte> ParamsRaw;
    public ReadOnlySpan<byte> Trailing;
    public bool HasTrailing;

    public IrcMessage ToMessage()
    {
        // Convert spans to strings and build IrcMessage
        var tags = Tags.IsEmpty ? null : System.Text.Encoding.UTF8.GetString(Tags);
        var prefix = Prefix.IsEmpty ? null : System.Text.Encoding.UTF8.GetString(Prefix);
        var command = System.Text.Encoding.UTF8.GetString(Command);

        var parameters = new List<string>();
        if (!ParamsRaw.IsEmpty)
        {
            // Split ParamsRaw by spaces
            var remaining = ParamsRaw;
            while (!remaining.IsEmpty)
            {
                int spaceIdx = remaining.IndexOf(IrcConstants.Space);
                if (spaceIdx < 0)
                {
                    parameters.Add(System.Text.Encoding.UTF8.GetString(remaining));
                    break;
                }
                if (spaceIdx > 0)
                    parameters.Add(System.Text.Encoding.UTF8.GetString(remaining[..spaceIdx]));
                remaining = remaining[(spaceIdx + 1)..];
            }
        }
        if (HasTrailing)
            parameters.Add(System.Text.Encoding.UTF8.GetString(Trailing));

        return new IrcMessage(tags, prefix, command, parameters);
    }
}
