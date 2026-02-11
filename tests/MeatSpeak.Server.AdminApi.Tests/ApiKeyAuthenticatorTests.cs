using Xunit;
using MeatSpeak.Server.AdminApi;
using MeatSpeak.Server.AdminApi.Auth;

namespace MeatSpeak.Server.AdminApi.Tests;

public class ApiKeyAuthenticatorTests
{
    [Fact]
    public void Authenticate_ValidKey_ReturnsTrue()
    {
        var hash = ApiKeyAuthenticator.GenerateHash("my-secret-key");
        var keys = new[] { new ApiKeyEntry { Name = "test", KeyHash = hash } };
        var auth = new ApiKeyAuthenticator(keys);
        Assert.True(auth.Authenticate("my-secret-key"));
    }

    [Fact]
    public void Authenticate_InvalidKey_ReturnsFalse()
    {
        var hash = ApiKeyAuthenticator.GenerateHash("my-secret-key");
        var keys = new[] { new ApiKeyEntry { Name = "test", KeyHash = hash } };
        var auth = new ApiKeyAuthenticator(keys);
        Assert.False(auth.Authenticate("wrong-key"));
    }

    [Fact]
    public void Authenticate_MethodRestriction_Allowed()
    {
        var hash = ApiKeyAuthenticator.GenerateHash("restricted-key");
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
        var hash = ApiKeyAuthenticator.GenerateHash("restricted-key");
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
    public void GenerateHash_ProducesArgon2idFormat()
    {
        var hash = ApiKeyAuthenticator.GenerateHash("test");
        Assert.StartsWith("$argon2id$", hash);
    }

    [Fact]
    public void GenerateHash_DifferentSaltsEachTime()
    {
        var hash1 = ApiKeyAuthenticator.GenerateHash("test");
        var hash2 = ApiKeyAuthenticator.GenerateHash("test");
        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public void Authenticate_EmptyKeys_ReturnsFalse()
    {
        var auth = new ApiKeyAuthenticator(Array.Empty<ApiKeyEntry>());
        Assert.False(auth.Authenticate("any-key"));
    }
}
