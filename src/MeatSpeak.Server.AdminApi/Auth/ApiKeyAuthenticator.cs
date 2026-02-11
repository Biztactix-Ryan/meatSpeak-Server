namespace MeatSpeak.Server.AdminApi.Auth;

using System.Security.Cryptography;
using System.Text;

public sealed class ApiKeyAuthenticator
{
    private readonly List<(ApiKeyEntry Entry, byte[] HashBytes)> _keys;
    private static readonly byte[] DummyHash = new byte[32]; // SHA-256 produces 32 bytes

    public ApiKeyAuthenticator(IEnumerable<ApiKeyEntry> keys)
    {
        _keys = keys.Select(entry => 
        {
            try
            {
                var hashBytes = Convert.FromHexString(entry.KeyHash);
                // Validate expected length (SHA-256 = 32 bytes)
                if (hashBytes.Length != 32)
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

        bool foundMatch = false;
        bool result = false;

        foreach (var (entry, entryHashBytes) in _keys)
        {
            // Always call FixedTimeEquals to maintain constant time, even for invalid entries
            bool hashMatches = CryptographicOperations.FixedTimeEquals(hashBytes, entryHashBytes);

            // Use conditional logic instead of early returns to maintain constant time
            if (hashMatches && !foundMatch)
            {
                foundMatch = true;
                
                // Check method permissions
                bool hasMethodRestriction = method != null && entry.AllowedMethods != null && entry.AllowedMethods.Count > 0;
                if (hasMethodRestriction)
                {
                    result = entry.AllowedMethods!.Contains(method!, StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    result = true;
                }
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
