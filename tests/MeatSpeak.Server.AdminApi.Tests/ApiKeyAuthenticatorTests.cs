using Xunit;
using MeatSpeak.Server.AdminApi.Auth;

namespace MeatSpeak.Server.AdminApi.Tests;

public class ApiKeyAuthenticatorTests
{
    [Fact]
    public void Authenticate_ValidKey_ReturnsTrue()
    {
        var hash = ApiKeyAuthenticator.HashKey("my-secret-key");
        var keys = new[] { new ApiKeyEntry { Name = "test", KeyHash = hash } };
        var auth = new ApiKeyAuthenticator(keys);
        Assert.True(auth.Authenticate("my-secret-key"));
    }

    [Fact]
    public void Authenticate_InvalidKey_ReturnsFalse()
    {
        var hash = ApiKeyAuthenticator.HashKey("my-secret-key");
        var keys = new[] { new ApiKeyEntry { Name = "test", KeyHash = hash } };
        var auth = new ApiKeyAuthenticator(keys);
        Assert.False(auth.Authenticate("wrong-key"));
    }

    [Fact]
    public void Authenticate_MethodRestriction_Allowed()
    {
        var hash = ApiKeyAuthenticator.HashKey("restricted-key");
        var keys = new[] { new ApiKeyEntry
        {
            Name = "restricted",
            KeyHash = hash,
            AllowedMethods = new List<string> { "server.stats", "user.list" }
        }};
        var auth = new ApiKeyAuthenticator(keys);
        Assert.True(auth.Authenticate("restricted-key", "server.stats"));
    }

    [Fact]
    public void Authenticate_MethodRestriction_Denied()
    {
        var hash = ApiKeyAuthenticator.HashKey("restricted-key");
        var keys = new[] { new ApiKeyEntry
        {
            Name = "restricted",
            KeyHash = hash,
            AllowedMethods = new List<string> { "server.stats" }
        }};
        var auth = new ApiKeyAuthenticator(keys);
        Assert.False(auth.Authenticate("restricted-key", "server.shutdown"));
    }

    [Fact]
    public void HashKey_ProducesDeterministicHash()
    {
        var hash1 = ApiKeyAuthenticator.HashKey("test");
        var hash2 = ApiKeyAuthenticator.HashKey("test");
        Assert.Equal(hash1, hash2);
        Assert.Equal(64, hash1.Length); // SHA-256 hex = 64 chars
    }

    [Fact]
    public void Authenticate_EmptyKeys_ReturnsFalse()
    {
        var auth = new ApiKeyAuthenticator(Array.Empty<ApiKeyEntry>());
        Assert.False(auth.Authenticate("any-key"));
    }

    [Fact]
    public void Authenticate_MultipleKeys_MatchesCorrectKey()
    {
        var hash1 = ApiKeyAuthenticator.HashKey("key1");
        var hash2 = ApiKeyAuthenticator.HashKey("key2");
        var hash3 = ApiKeyAuthenticator.HashKey("key3");
        
        var keys = new[]
        {
            new ApiKeyEntry { Name = "first", KeyHash = hash1 },
            new ApiKeyEntry { Name = "second", KeyHash = hash2 },
            new ApiKeyEntry { Name = "third", KeyHash = hash3 }
        };
        
        var auth = new ApiKeyAuthenticator(keys);
        Assert.True(auth.Authenticate("key2"));
        Assert.False(auth.Authenticate("invalid-key"));
    }

    [Fact]
    public void Authenticate_MultipleKeysWithSameHash_UsesFirstMatchPermissions()
    {
        var hash = ApiKeyAuthenticator.HashKey("shared-key");
        
        var keys = new[]
        {
            new ApiKeyEntry
            {
                Name = "restricted",
                KeyHash = hash,
                AllowedMethods = new List<string> { "server.stats" }
            },
            new ApiKeyEntry
            {
                Name = "unrestricted",
                KeyHash = hash
            }
        };
        
        var auth = new ApiKeyAuthenticator(keys);
        // Should use first match, which has restrictions
        Assert.True(auth.Authenticate("shared-key", "server.stats"));
        Assert.False(auth.Authenticate("shared-key", "server.shutdown"));
    }

    [Fact]
    public void Authenticate_MalformedHash_SkipsInvalidEntry()
    {
        var validHash = ApiKeyAuthenticator.HashKey("valid-key");
        
        var keys = new[]
        {
            new ApiKeyEntry { Name = "invalid", KeyHash = "not-a-valid-hex-string" },
            new ApiKeyEntry { Name = "valid", KeyHash = validHash }
        };
        
        var auth = new ApiKeyAuthenticator(keys);
        Assert.True(auth.Authenticate("valid-key"));
        Assert.False(auth.Authenticate("invalid-key"));
    }
}
