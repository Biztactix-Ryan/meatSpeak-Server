namespace MeatSpeak.Server.AdminApi.Auth;

using System.Security.Cryptography;
using System.Text;

public sealed class ApiKeyAuthenticator
{
    private readonly List<(ApiKeyEntry Entry, byte[] HashBytes)> _keys;

    public ApiKeyAuthenticator(IEnumerable<ApiKeyEntry> keys)
    {
        _keys = keys.Select(entry => 
        {
            try
            {
                var hashBytes = Convert.FromHexString(entry.KeyHash);
                return (entry, hashBytes);
            }
            catch (FormatException)
            {
                // Skip malformed hash entries
                return (entry, Array.Empty<byte>());
            }
        }).ToList();
    }

    public bool Authenticate(string apiKey, string? method = null)
    {
        var hash = HashKey(apiKey);
        var hashBytes = Convert.FromHexString(hash);

        bool foundMatch = false;
        bool result = false;

        foreach (var (entry, entryHashBytes) in _keys)
        {
            // Skip entries with invalid hash format
            if (entryHashBytes.Length == 0)
                continue;

            // FixedTimeEquals requires equal length arrays; use standard comparison to avoid exception
            bool hashMatches = hashBytes.Length == entryHashBytes.Length 
                && CryptographicOperations.FixedTimeEquals(hashBytes, entryHashBytes);

            if (hashMatches && !foundMatch)
            {
                foundMatch = true;
                if (method != null && entry.AllowedMethods != null && entry.AllowedMethods.Count > 0)
                    result = entry.AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase);
                else
                    result = true;
            }
        }

        return result;
    }

    public static string HashKey(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
