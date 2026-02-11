namespace MeatSpeak.Server.AdminApi.Auth;

using System.Security.Cryptography;
using System.Text;

public sealed class ApiKeyAuthenticator
{
    private const int Sha256HashLengthBytes = 32;
    
    private readonly List<(ApiKeyEntry Entry, byte[] HashBytes)> _keys;
    private static readonly byte[] DummyHash = new byte[Sha256HashLengthBytes];

    public ApiKeyAuthenticator(IEnumerable<ApiKeyEntry> keys)
    {
        _keys = keys.Select(entry => 
        {
            try
            {
                var hashBytes = Convert.FromHexString(entry.KeyHash);
                // Validate expected length (SHA-256 = 32 bytes)
                if (hashBytes.Length != Sha256HashLengthBytes)
                    return (entry, DummyHash);
                return (entry, hashBytes);
            }
            catch (FormatException)
            {
                // Use dummy hash for malformed entries to maintain constant time
                return (entry, DummyHash);
            }
        }).ToList();
    }

    public bool Authenticate(string apiKey, string? method = null)
    {
        var hash = HashKey(apiKey);
        var hashBytes = Convert.FromHexString(hash);

        ApiKeyEntry? matchedEntry = null;
        bool foundMatch = false;

        // Iterate through all keys with constant-time comparison
        foreach (var (entry, entryHashBytes) in _keys)
        {
            bool hashMatches = CryptographicOperations.FixedTimeEquals(hashBytes, entryHashBytes);

            // Use conditional assignment without short-circuit evaluation
            // Only set matchedEntry on first match to enforce "first match wins" policy
            if (hashMatches & !foundMatch)
            {
                matchedEntry = entry;
                foundMatch = true;
            }
        }

        // No match found
        if (matchedEntry == null)
            return false;

        // Check method permissions only after constant-time loop completes
        if (method != null && matchedEntry.AllowedMethods != null && matchedEntry.AllowedMethods.Count > 0)
        {
            return matchedEntry.AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase);
        }

        return true;
    }

    public static string HashKey(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
