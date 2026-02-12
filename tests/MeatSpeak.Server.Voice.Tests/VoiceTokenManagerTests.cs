using Xunit;

namespace MeatSpeak.Server.Voice.Tests;

public class VoiceTokenManagerTests
{
    // ─── GenerateToken ───

    [Fact]
    public void GenerateToken_ReturnsNonNullNonEmptyString()
    {
        var manager = new VoiceTokenManager();

        var token = manager.GenerateToken("session1", "#voice");

        Assert.False(string.IsNullOrEmpty(token));
    }

    [Fact]
    public void GenerateToken_FormatIsSessionId_Channel_Timestamp_Base64()
    {
        var manager = new VoiceTokenManager();

        var token = manager.GenerateToken("session1", "#voice");

        var parts = token.Split(':');
        Assert.True(parts.Length >= 4);
        Assert.Equal("session1", parts[0]);
        Assert.Equal("#voice", parts[1]);
        Assert.True(long.TryParse(parts[2], out _), "Third part should be a unix timestamp");
        // Fourth part should be valid base64
        var macBytes = Convert.FromBase64String(parts[3]);
        Assert.NotEmpty(macBytes);
    }

    // ─── ValidateToken ───

    [Fact]
    public void ValidateToken_ReturnsTrueForValidToken()
    {
        var manager = new VoiceTokenManager();

        var token = manager.GenerateToken("session1", "#voice");

        Assert.True(manager.ValidateToken(token, "session1", "#voice"));
    }

    [Fact]
    public void ValidateToken_ReturnsFalseForWrongSessionId()
    {
        var manager = new VoiceTokenManager();

        var token = manager.GenerateToken("session1", "#voice");

        Assert.False(manager.ValidateToken(token, "session2", "#voice"));
    }

    [Fact]
    public void ValidateToken_ReturnsFalseForWrongChannel()
    {
        var manager = new VoiceTokenManager();

        var token = manager.GenerateToken("session1", "#voice");

        Assert.False(manager.ValidateToken(token, "session1", "#other"));
    }

    [Fact]
    public void ValidateToken_ReturnsFalseForTamperedMac()
    {
        var manager = new VoiceTokenManager();

        var token = manager.GenerateToken("session1", "#voice");
        var parts = token.Split(':');
        // Replace the MAC with a different base64 value
        var tamperedMac = Convert.ToBase64String(new byte[32]);
        var tampered = $"{parts[0]}:{parts[1]}:{parts[2]}:{tamperedMac}";

        Assert.False(manager.ValidateToken(tampered, "session1", "#voice"));
    }

    [Fact]
    public void ValidateToken_ReturnsFalseForTooFewParts()
    {
        var manager = new VoiceTokenManager();

        Assert.False(manager.ValidateToken("session1:#voice", "session1", "#voice"));
    }

    [Fact]
    public void ValidateToken_ReturnsFalseForEmptyString()
    {
        var manager = new VoiceTokenManager();

        Assert.False(manager.ValidateToken("", "session1", "#voice"));
    }

    // ─── Cross-manager validation ───

    [Fact]
    public void TwoManagersWithDifferentKeys_RejectEachOthersTokens()
    {
        var key1 = new byte[32];
        var key2 = new byte[32];
        key2[0] = 0xFF; // Ensure different keys
        var manager1 = new VoiceTokenManager(key1);
        var manager2 = new VoiceTokenManager(key2);

        var token1 = manager1.GenerateToken("session1", "#voice");
        var token2 = manager2.GenerateToken("session1", "#voice");

        Assert.False(manager2.ValidateToken(token1, "session1", "#voice"));
        Assert.False(manager1.ValidateToken(token2, "session1", "#voice"));
    }

    [Fact]
    public void SameManager_ValidatesItsOwnTokens()
    {
        var manager = new VoiceTokenManager();

        var token1 = manager.GenerateToken("sessionA", "#channel1");
        var token2 = manager.GenerateToken("sessionB", "#channel2");

        Assert.True(manager.ValidateToken(token1, "sessionA", "#channel1"));
        Assert.True(manager.ValidateToken(token2, "sessionB", "#channel2"));
    }
}
