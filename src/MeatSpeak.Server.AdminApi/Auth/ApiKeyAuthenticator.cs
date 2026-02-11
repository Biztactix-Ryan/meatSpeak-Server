namespace MeatSpeak.Server.AdminApi.Auth;

using System.Security.Cryptography;
using System.Text;

public sealed class ApiKeyAuthenticator
{
    private readonly List<ApiKeyEntry> _keys;

    public ApiKeyAuthenticator(IEnumerable<ApiKeyEntry> keys)
    {
        _keys = keys.ToList();
    }

    public bool Authenticate(string apiKey, string? method = null)
    {
        var hash = HashKey(apiKey);
        var hashBytes = Convert.FromHexString(hash);

        bool foundMatch = false;
        bool result = false;

        foreach (var entry in _keys)
        {
            var entryHashBytes = Convert.FromHexString(entry.KeyHash);
            bool hashMatches = CryptographicOperations.FixedTimeEquals(hashBytes, entryHashBytes);

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
