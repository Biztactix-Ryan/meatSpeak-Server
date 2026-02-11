namespace MeatSpeak.Protocol;

using System.Text;

public static class MessageBuilder
{
    public static int Write(Span<byte> buffer, string? prefix, string command, ReadOnlySpan<string> parameters)
    {
        int pos = 0;

        if (prefix != null)
        {
            buffer[pos++] = IrcConstants.Colon;
            pos += Encoding.UTF8.GetBytes(prefix, buffer[pos..]);
            buffer[pos++] = IrcConstants.Space;
        }

        pos += Encoding.UTF8.GetBytes(command, buffer[pos..]);

        for (int i = 0; i < parameters.Length; i++)
        {
            buffer[pos++] = IrcConstants.Space;
            bool isLast = i == parameters.Length - 1;
            if (isLast && (parameters[i].Contains(' ') || parameters[i].Length == 0 || parameters[i][0] == ':'))
                buffer[pos++] = IrcConstants.Colon;
            pos += Encoding.UTF8.GetBytes(parameters[i], buffer[pos..]);
        }

        buffer[pos++] = IrcConstants.CR;
        buffer[pos++] = IrcConstants.LF;

        return pos;
    }

    // Convenience overload with params array
    public static int Write(Span<byte> buffer, string? prefix, string command, params string[] parameters)
        => Write(buffer, prefix, command, parameters.AsSpan());

    // Overload that writes numeric as 3-digit string
    public static int WriteNumeric(Span<byte> buffer, string serverName, int numeric, string target, ReadOnlySpan<string> parameters)
    {
        // Build: :serverName 001 target params...
        int pos = 0;
        buffer[pos++] = IrcConstants.Colon;
        pos += Encoding.UTF8.GetBytes(serverName, buffer[pos..]);
        buffer[pos++] = IrcConstants.Space;

        var numStr = Numerics.Format(numeric);
        pos += Encoding.UTF8.GetBytes(numStr, buffer[pos..]);
        buffer[pos++] = IrcConstants.Space;

        pos += Encoding.UTF8.GetBytes(target, buffer[pos..]);

        for (int i = 0; i < parameters.Length; i++)
        {
            buffer[pos++] = IrcConstants.Space;
            bool isLast = i == parameters.Length - 1;
            if (isLast)
                buffer[pos++] = IrcConstants.Colon;
            pos += Encoding.UTF8.GetBytes(parameters[i], buffer[pos..]);
        }

        buffer[pos++] = IrcConstants.CR;
        buffer[pos++] = IrcConstants.LF;

        return pos;
    }

    public static int WriteNumeric(Span<byte> buffer, string serverName, int numeric, string target, params string[] parameters)
        => WriteNumeric(buffer, serverName, numeric, target, parameters.AsSpan());
}
