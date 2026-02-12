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

    public bool ValidateToken(string token, string sessionId, string channel, TimeSpan? maxAge = null)
    {
        // Token format: sessionId:channel:timestamp:base64mac
        // Channel names can contain colons, so find the last colon for MAC
        var lastColon = token.LastIndexOf(':');
        if (lastColon < 0) return false;

        var macStr = token[(lastColon + 1)..];
        var payloadAndTimestamp = token[..lastColon];

        // Find the timestamp (second-to-last colon)
        var tsColon = payloadAndTimestamp.LastIndexOf(':');
        if (tsColon < 0) return false;

        var timestampStr = payloadAndTimestamp[(tsColon + 1)..];
        var prefix = payloadAndTimestamp[..tsColon];

        // prefix should be "sessionId:channel"
        var firstColon = prefix.IndexOf(':');
        if (firstColon < 0) return false;

        var tokenSessionId = prefix[..firstColon];
        var tokenChannel = prefix[(firstColon + 1)..];

        if (tokenSessionId != sessionId || tokenChannel != channel) return false;

        // Check expiration
        if (!long.TryParse(timestampStr, out var timestamp)) return false;
        var tokenAge = maxAge ?? TimeSpan.FromHours(1);
        var tokenTime = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        if (DateTimeOffset.UtcNow - tokenTime > tokenAge) return false;

        // Validate HMAC
        var payload = $"{tokenSessionId}:{tokenChannel}:{timestampStr}";
        var expectedMac = HMACSHA256.HashData(_secretKey, Encoding.UTF8.GetBytes(payload));
        byte[] actualMac;
        try { actualMac = Convert.FromBase64String(macStr); }
        catch (FormatException) { return false; }
        return CryptographicOperations.FixedTimeEquals(expectedMac, actualMac);
    }
}
