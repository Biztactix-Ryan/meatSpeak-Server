namespace MeatSpeak.Server.AdminApi.Auth;

public sealed class ApiKeyAuthenticator
{
    private readonly List<ApiKeyEntry> _keys;

    public ApiKeyAuthenticator(IEnumerable<ApiKeyEntry> keys)
    {
        _keys = keys.ToList();
    }

    public bool Authenticate(string apiKey, string? method = null)
    {
        foreach (var entry in _keys)
        {
            if (PasswordHasher.VerifyPassword(apiKey, entry.KeyHash))
            {
                if (method != null && entry.AllowedMethods != null && entry.AllowedMethods.Count > 0)
                    return entry.AllowedMethods.Contains(method, StringComparer.OrdinalIgnoreCase);
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Generates an Argon2id hash suitable for storing in configuration.
    /// </summary>
    public static string GenerateHash(string apiKey)
    {
        return PasswordHasher.HashPassword(apiKey);
    }
}
