namespace MeatSpeak.Server;

/// <summary>
/// Matches IRC wildcard patterns against input strings.
/// Supports '*' (zero or more characters) and '?' (exactly one character).
/// Comparison is case-insensitive.
/// </summary>
internal static class IrcWildcard
{
    public static bool Match(string pattern, string input)
    {
        // Iterative two-pointer algorithm with backtracking for '*'
        int pIdx = 0, iIdx = 0;
        int starIdx = -1, matchIdx = 0;

        while (iIdx < input.Length)
        {
            if (pIdx < pattern.Length &&
                (char.ToLowerInvariant(pattern[pIdx]) == char.ToLowerInvariant(input[iIdx]) ||
                 pattern[pIdx] == '?'))
            {
                pIdx++;
                iIdx++;
            }
            else if (pIdx < pattern.Length && pattern[pIdx] == '*')
            {
                starIdx = pIdx;
                matchIdx = iIdx;
                pIdx++;
            }
            else if (starIdx >= 0)
            {
                pIdx = starIdx + 1;
                matchIdx++;
                iIdx = matchIdx;
            }
            else
            {
                return false;
            }
        }

        while (pIdx < pattern.Length && pattern[pIdx] == '*')
        {
            pIdx++;
        }

        return pIdx == pattern.Length;
    }
}
