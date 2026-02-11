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

        foreach (var entry in _keys)
        {
            if (string.Equals(entry.KeyHash, hash, StringComparison.OrdinalIgnoreCase))
            {
                if (method != null && entry.AllowedMethods != null && entry.AllowedMethods.Count > 0)
                    return entry.AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase);
                return true;
            }
        }

        return false;
    }

    public static string HashKey(string apiKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
