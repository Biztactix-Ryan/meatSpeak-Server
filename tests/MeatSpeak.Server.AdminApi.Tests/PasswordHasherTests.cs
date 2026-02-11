using Xunit;
using MeatSpeak.Server.AdminApi.Auth;

namespace MeatSpeak.Server.AdminApi.Tests;

public class PasswordHasherTests
{
    [Fact]
    public void HashPassword_ProducesPhcString()
    {
        var hash = PasswordHasher.HashPassword("test-password");
        Assert.StartsWith("$argon2id$v=19$m=65536,t=3,p=1$", hash);
    }

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = PasswordHasher.HashPassword("my-password");
        Assert.True(PasswordHasher.VerifyPassword("my-password", hash));
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = PasswordHasher.HashPassword("my-password");
        Assert.False(PasswordHasher.VerifyPassword("wrong-password", hash));
    }

    [Fact]
    public void VerifyPassword_InvalidHash_ReturnsFalse()
    {
        Assert.False(PasswordHasher.VerifyPassword("test", "not-a-valid-hash"));
    }

    [Fact]
    public void HashPassword_UniqueSalts()
    {
        var hash1 = PasswordHasher.HashPassword("same");
        var hash2 = PasswordHasher.HashPassword("same");
        Assert.NotEqual(hash1, hash2);
        Assert.True(PasswordHasher.VerifyPassword("same", hash1));
        Assert.True(PasswordHasher.VerifyPassword("same", hash2));
    }
}
