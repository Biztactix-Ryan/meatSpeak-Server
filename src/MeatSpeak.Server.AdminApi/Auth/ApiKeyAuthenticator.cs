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
            // Always perform comparison, even for invalid entries, to maintain constant time
            bool isValidEntry = entryHashBytes.Length > 0;
            bool lengthMatches = hashBytes.Length == entryHashBytes.Length;
            
            // Only call FixedTimeEquals if lengths match to avoid exception
            bool hashMatches = false;
            if (lengthMatches && isValidEntry)
            {
                hashMatches = CryptographicOperations.FixedTimeEquals(hashBytes, entryHashBytes);
            }

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
