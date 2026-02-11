namespace MeatSpeak.Server.AdminApi.Auth;

using System.Security.Cryptography;
using Konscious.Security.Cryptography;

/// <summary>
/// Argon2id password hasher using PHC string format.
/// Parameters: 64 MB memory, 3 iterations, 1 parallelism, 32-byte hash, 16-byte salt.
/// </summary>
public static class PasswordHasher
{
    private const int MemorySize = 65536; // 64 MB
    private const int Iterations = 3;
    private const int Parallelism = 1;
    private const int HashLength = 32;
    private const int SaltLength = 16;

    /// <summary>
    /// Hashes a password using Argon2id and returns a PHC-format string.
    /// </summary>
    public static string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var hash = DeriveHash(password, salt);
        return $"$argon2id$v=19$m={MemorySize},t={Iterations},p={Parallelism}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    /// <summary>
    /// Verifies a password against a stored PHC-format hash string.
    /// </summary>
    public static bool VerifyPassword(string password, string storedHash)
    {
        if (!TryParsePhcString(storedHash, out var salt, out var expectedHash))
            return false;

        var actualHash = DeriveHash(password, salt);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }

    private static byte[] DeriveHash(string password, byte[] salt)
    {
        using var argon2 = new Argon2id(System.Text.Encoding.UTF8.GetBytes(password));
        argon2.Salt = salt;
        argon2.MemorySize = MemorySize;
        argon2.Iterations = Iterations;
        argon2.DegreeOfParallelism = Parallelism;
        return argon2.GetBytes(HashLength);
    }

    private static bool TryParsePhcString(string phc, out byte[] salt, out byte[] hash)
    {
        salt = Array.Empty<byte>();
        hash = Array.Empty<byte>();

        // Format: $argon2id$v=19$m=65536,t=3,p=1$<salt-b64>$<hash-b64>
        if (!phc.StartsWith("$argon2id$"))
            return false;

        var parts = phc.Split('$');
        // parts[0] = "", parts[1] = "argon2id", parts[2] = "v=19", parts[3] = "m=...,t=...,p=...", parts[4] = salt, parts[5] = hash
        if (parts.Length != 6)
            return false;

        try
        {
            salt = Convert.FromBase64String(parts[4]);
            hash = Convert.FromBase64String(parts[5]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
