namespace MeatSpeak.Server.Voice;

using System.Security.Cryptography;
using System.Text;

public sealed class VoiceTokenManager
{
    private readonly byte[] _secretKey;

    public VoiceTokenManager(byte[]? secretKey = null)
    {
        _secretKey = secretKey ?? RandomNumberGenerator.GetBytes(32);
    }

    public string GenerateToken(string sessionId, string channel)
    {
        var payload = $"{sessionId}:{channel}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        var mac = HMACSHA256.HashData(_secretKey, Encoding.UTF8.GetBytes(payload));
        return $"{payload}:{Convert.ToBase64String(mac)}";
    }

    public bool ValidateToken(string token, string sessionId, string channel)
    {
        var parts = token.Split(':');
        if (parts.Length < 4) return false;
        if (parts[0] != sessionId || parts[1] != channel) return false;

        var payload = $"{parts[0]}:{parts[1]}:{parts[2]}";
        var expectedMac = HMACSHA256.HashData(_secretKey, Encoding.UTF8.GetBytes(payload));
        var actualMac = Convert.FromBase64String(parts[3]);
        return CryptographicOperations.FixedTimeEquals(expectedMac, actualMac);
    }
}
