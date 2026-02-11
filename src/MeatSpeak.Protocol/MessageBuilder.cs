namespace MeatSpeak.Protocol;

using System.Text;

public static class MessageBuilder
{
    /// <summary>
    /// Tries to write an IRC message to the buffer. Returns the number of bytes written, or -1 if the buffer is too small.
    /// </summary>
    public static int Write(Span<byte> buffer, string? prefix, string command, ReadOnlySpan<string> parameters)
    {
        int pos = 0;

        try
        {
            if (prefix != null)
            {
                // Check space for ":prefix "
                if (pos >= buffer.Length) return -1;
                buffer[pos++] = IrcConstants.Colon;
                
                int bytesWritten = Encoding.UTF8.GetBytes(prefix, buffer[pos..]);
                pos += bytesWritten;
                
                if (pos >= buffer.Length) return -1;
                buffer[pos++] = IrcConstants.Space;
            }

            // Write command
            int cmdBytes = Encoding.UTF8.GetBytes(command, buffer[pos..]);
            pos += cmdBytes;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (pos >= buffer.Length) return -1;
                buffer[pos++] = IrcConstants.Space;
                
                bool isLast = i == parameters.Length - 1;
                if (isLast && (parameters[i].Contains(' ') || parameters[i].Length == 0 || parameters[i][0] == ':'))
                {
                    if (pos >= buffer.Length) return -1;
                    buffer[pos++] = IrcConstants.Colon;
                }
                
                int paramBytes = Encoding.UTF8.GetBytes(parameters[i], buffer[pos..]);
                pos += paramBytes;
            }

            // Write CRLF
            if (pos + 2 > buffer.Length) return -1;
            buffer[pos++] = IrcConstants.CR;
            buffer[pos++] = IrcConstants.LF;

            return pos;
        }
        catch (ArgumentException)
        {
            // Buffer too small
            return -1;
        }
    }

    // Convenience overload with params array
    public static int Write(Span<byte> buffer, string? prefix, string command, params string[] parameters)
        => Write(buffer, prefix, command, parameters.AsSpan());

    // Overload that writes numeric as 3-digit string
    /// <summary>
    /// Tries to write an IRC numeric message to the buffer. Returns the number of bytes written, or -1 if the buffer is too small.
    /// </summary>
    public static int WriteNumeric(Span<byte> buffer, string serverName, int numeric, string target, ReadOnlySpan<string> parameters)
    {
        try
        {
            // Build: :serverName 001 target params...
            int pos = 0;
            
            if (pos >= buffer.Length) return -1;
            buffer[pos++] = IrcConstants.Colon;
            
            int serverBytes = Encoding.UTF8.GetBytes(serverName, buffer[pos..]);
            pos += serverBytes;
            
            if (pos >= buffer.Length) return -1;
            buffer[pos++] = IrcConstants.Space;

            var numStr = Numerics.Format(numeric);
            int numBytes = Encoding.UTF8.GetBytes(numStr, buffer[pos..]);
            pos += numBytes;
            
            if (pos >= buffer.Length) return -1;
            buffer[pos++] = IrcConstants.Space;

            int targetBytes = Encoding.UTF8.GetBytes(target, buffer[pos..]);
            pos += targetBytes;

            for (int i = 0; i < parameters.Length; i++)
            {
                if (pos >= buffer.Length) return -1;
                buffer[pos++] = IrcConstants.Space;
                
                bool isLast = i == parameters.Length - 1;
                if (isLast)
                {
                    if (pos >= buffer.Length) return -1;
                    buffer[pos++] = IrcConstants.Colon;
                }
                
                int paramBytes = Encoding.UTF8.GetBytes(parameters[i], buffer[pos..]);
                pos += paramBytes;
            }

            // Write CRLF
            if (pos + 2 > buffer.Length) return -1;
            buffer[pos++] = IrcConstants.CR;
            buffer[pos++] = IrcConstants.LF;

            return pos;
        }
        catch (ArgumentException)
        {
            // Buffer too small
            return -1;
        }
    }

    public static int WriteNumeric(Span<byte> buffer, string serverName, int numeric, string target, params string[] parameters)
        => WriteNumeric(buffer, serverName, numeric, target, parameters.AsSpan());
}
