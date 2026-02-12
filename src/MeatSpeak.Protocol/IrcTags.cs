namespace MeatSpeak.Protocol;

public static class IrcTags
{
    public static Dictionary<string, string?> Parse(string? tagsString)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(tagsString))
            return result;

        foreach (var tag in tagsString.Split(';'))
        {
            if (tag.Length == 0) continue;
            int eqIdx = tag.IndexOf('=');
            if (eqIdx < 0)
            {
                result[tag] = null;
            }
            else
            {
                var key = tag[..eqIdx];
                var value = UnescapeValue(tag[(eqIdx + 1)..]);
                result[key] = value;
            }
        }

        return result;
    }

    public static string Serialize(IReadOnlyDictionary<string, string?> tags)
    {
        return string.Join(";", tags.Select(kvp =>
            kvp.Value != null ? $"{kvp.Key}={EscapeValue(kvp.Value)}" : kvp.Key));
    }

    private static string UnescapeValue(string value)
    {
        // IRCv3 tag escaping: \: -> ; \s -> space \\ -> \ \r -> CR \n -> LF
        if (!value.Contains('\\')) return value;
        var sb = new System.Text.StringBuilder(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                i++;
                sb.Append(value[i] switch
                {
                    ':' => ';',
                    's' => ' ',
                    '\\' => '\\',
                    'r' => '\r',
                    'n' => '\n',
                    _ => value[i],
                });
            }
            else
            {
                sb.Append(value[i]);
            }
        }
        return sb.ToString();
    }

    private static string EscapeValue(string value)
    {
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (char c in value)
        {
            sb.Append(c switch
            {
                ';' => "\\:",
                ' ' => "\\s",
                '\\' => "\\\\",
                '\r' => "\\r",
                '\n' => "\\n",
                _ => c.ToString(),
            });
        }
        return sb.ToString();
    }
}
