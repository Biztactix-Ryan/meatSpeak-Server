namespace MeatSpeak.Server.Transport.Tcp;

/// <summary>
/// Scans a byte buffer for CRLF-delimited IRC lines.
/// Returns complete lines via callback and reports the number of bytes consumed.
/// </summary>
public static class LineFramer
{
    public delegate void LineCallback(ReadOnlySpan<byte> line);

    /// <summary>
    /// Scans buffer[offset..offset+count] for complete lines ending in \r\n or \n.
    /// Invokes callback for each complete line (without the line ending).
    /// Returns the number of bytes consumed (including line endings).
    /// Remaining unconsumed bytes should be compacted to buffer start by caller.
    /// </summary>
    public static int Scan(byte[] buffer, int offset, int count, LineCallback callback)
    {
        int consumed = 0;
        int searchStart = offset;
        int end = offset + count;

        while (searchStart < end)
        {
            // Find \n
            int lfIndex = -1;
            for (int i = searchStart; i < end; i++)
            {
                if (buffer[i] == (byte)'\n')
                {
                    lfIndex = i;
                    break;
                }
            }

            if (lfIndex < 0)
                break; // No complete line

            // Determine line end (strip \r before \n if present)
            int lineEnd = lfIndex;
            if (lineEnd > searchStart && buffer[lineEnd - 1] == (byte)'\r')
                lineEnd--;

            int lineLength = lineEnd - searchStart;
            if (lineLength > 0)
            {
                var lineSpan = new ReadOnlySpan<byte>(buffer, searchStart, lineLength);
                callback(lineSpan);
            }

            consumed += (lfIndex - searchStart) + 1; // +1 for \n
            searchStart = lfIndex + 1;
        }

        return consumed;
    }
}
