using Xunit;
using MeatSpeak.Server.State;
using MeatSpeak.Server.Core.Channels;

namespace MeatSpeak.Server.Tests;

public class ChannelImplTests
{
    private static ChannelMembership CreateMembership(string nickname, bool isOp = false, bool hasVoice = false)
    {
        return new ChannelMembership
        {
            Nickname = nickname,
            IsOperator = isOp,
            HasVoice = hasVoice,
            JoinedAt = DateTimeOffset.UtcNow,
        };
    }

    private static BanEntry CreateBan(string mask, string setBy = "oper!oper@host")
    {
        return new BanEntry(mask, setBy, DateTimeOffset.UtcNow);
    }

    // 1. Constructor sets name and default modes (n, t)
    [Fact]
    public void Constructor_SetsNameAndDefaultModes()
    {
        var channel = new ChannelImpl("#test");

        Assert.Equal("#test", channel.Name);
        Assert.Contains('n', channel.Modes);
        Assert.Contains('t', channel.Modes);
        Assert.Equal(2, channel.Modes.Count);
    }

    // 2. AddMember succeeds and IsMember returns true
    [Fact]
    public void AddMember_Succeeds_And_IsMember_ReturnsTrue()
    {
        var channel = new ChannelImpl("#test");
        var membership = CreateMembership("Alice");

        var result = channel.AddMember("Alice", membership);

        Assert.True(result);
        Assert.True(channel.IsMember("Alice"));
    }

    // 3. AddMember duplicate returns false
    [Fact]
    public void AddMember_Duplicate_ReturnsFalse()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", CreateMembership("Alice"));

        var result = channel.AddMember("Alice", CreateMembership("Alice"));

        Assert.False(result);
    }

    // 4. RemoveMember succeeds
    [Fact]
    public void RemoveMember_ExistingMember_ReturnsTrue()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", CreateMembership("Alice"));

        var result = channel.RemoveMember("Alice");

        Assert.True(result);
        Assert.False(channel.IsMember("Alice"));
    }

    // 5. RemoveMember unknown returns false
    [Fact]
    public void RemoveMember_UnknownMember_ReturnsFalse()
    {
        var channel = new ChannelImpl("#test");

        var result = channel.RemoveMember("Nobody");

        Assert.False(result);
    }

    // 6. GetMember returns membership
    [Fact]
    public void GetMember_ExistingMember_ReturnsMembership()
    {
        var channel = new ChannelImpl("#test");
        var membership = CreateMembership("Alice", isOp: true);
        channel.AddMember("Alice", membership);

        var result = channel.GetMember("Alice");

        Assert.NotNull(result);
        Assert.Equal("Alice", result.Nickname);
        Assert.True(result.IsOperator);
    }

    // 7. GetMember unknown returns null
    [Fact]
    public void GetMember_UnknownMember_ReturnsNull()
    {
        var channel = new ChannelImpl("#test");

        var result = channel.GetMember("Nobody");

        Assert.Null(result);
    }

    // 8. IsMember is case-insensitive
    [Fact]
    public void IsMember_IsCaseInsensitive()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", CreateMembership("Alice"));

        Assert.True(channel.IsMember("alice"));
        Assert.True(channel.IsMember("ALICE"));
        Assert.True(channel.IsMember("aLiCe"));
    }

    // 9. Members dictionary reflects added members
    [Fact]
    public void Members_ReflectsAddedMembers()
    {
        var channel = new ChannelImpl("#test");
        channel.AddMember("Alice", CreateMembership("Alice"));
        channel.AddMember("Bob", CreateMembership("Bob"));

        Assert.Equal(2, channel.Members.Count);
        Assert.True(channel.Members.ContainsKey("Alice"));
        Assert.True(channel.Members.ContainsKey("Bob"));
    }

    // 10. Topic can be set and retrieved
    [Fact]
    public void Topic_CanBeSetAndRetrieved()
    {
        var channel = new ChannelImpl("#test");

        Assert.Null(channel.Topic);

        channel.Topic = "Welcome to #test";
        channel.TopicSetBy = "Alice!alice@host";
        channel.TopicSetAt = DateTimeOffset.UtcNow;

        Assert.Equal("Welcome to #test", channel.Topic);
        Assert.Equal("Alice!alice@host", channel.TopicSetBy);
        Assert.NotNull(channel.TopicSetAt);
    }

    // 11. AddBan and IsBanned works
    [Fact]
    public void AddBan_And_IsBanned_Works()
    {
        var channel = new ChannelImpl("#test");
        var ban = CreateBan("*!*@bad.host");

        channel.AddBan(ban);

        Assert.True(channel.IsBanned("*!*@bad.host"));
    }

    // 12. IsBanned is case-insensitive on mask
    [Fact]
    public void IsBanned_IsCaseInsensitive()
    {
        var channel = new ChannelImpl("#test");
        channel.AddBan(CreateBan("*!*@Bad.Host"));

        Assert.True(channel.IsBanned("*!*@bad.host"));
        Assert.True(channel.IsBanned("*!*@BAD.HOST"));
    }

    // 13. RemoveBan removes ban
    [Fact]
    public void RemoveBan_ExistingBan_ReturnsTrue()
    {
        var channel = new ChannelImpl("#test");
        channel.AddBan(CreateBan("*!*@bad.host"));

        var result = channel.RemoveBan("*!*@bad.host");

        Assert.True(result);
        Assert.False(channel.IsBanned("*!*@bad.host"));
    }

    // 14. RemoveBan unknown mask returns false
    [Fact]
    public void RemoveBan_UnknownMask_ReturnsFalse()
    {
        var channel = new ChannelImpl("#test");

        var result = channel.RemoveBan("*!*@nonexistent");

        Assert.False(result);
    }

    // 15. Bans property returns copy of ban list
    [Fact]
    public void Bans_ReturnsCopyOfList()
    {
        var channel = new ChannelImpl("#test");
        var ban = CreateBan("*!*@bad.host");
        channel.AddBan(ban);

        var bans = channel.Bans;

        Assert.Single(bans);
        Assert.Equal("*!*@bad.host", bans[0].Mask);

        // Adding another ban should not affect the previously retrieved list
        channel.AddBan(CreateBan("*!*@other.host"));

        Assert.Single(bans);
        Assert.Equal(2, channel.Bans.Count);
    }

    // 16. AddExcept and IsExcepted works
    [Fact]
    public void AddExcept_And_IsExcepted_Works()
    {
        var channel = new ChannelImpl("#test");
        var except = CreateBan("friend!*@*");

        channel.AddExcept(except);

        Assert.True(channel.IsExcepted("friend!*@*"));
    }

    // 17. RemoveExcept works
    [Fact]
    public void RemoveExcept_ExistingExcept_ReturnsTrue()
    {
        var channel = new ChannelImpl("#test");
        channel.AddExcept(CreateBan("friend!*@*"));

        var result = channel.RemoveExcept("friend!*@*");

        Assert.True(result);
        Assert.False(channel.IsExcepted("friend!*@*"));
    }

    // 18. RemoveExcept unknown returns false
    [Fact]
    public void RemoveExcept_UnknownMask_ReturnsFalse()
    {
        var channel = new ChannelImpl("#test");

        var result = channel.RemoveExcept("nobody!*@*");

        Assert.False(result);
    }

    // 19. AddInvite and IsInvited works
    [Fact]
    public void AddInvite_And_IsInvited_Works()
    {
        var channel = new ChannelImpl("#test");

        channel.AddInvite("Alice");

        Assert.True(channel.IsInvited("Alice"));
    }

    // 20. IsInvited is case-insensitive
    [Fact]
    public void IsInvited_IsCaseInsensitive()
    {
        var channel = new ChannelImpl("#test");
        channel.AddInvite("Alice");

        Assert.True(channel.IsInvited("alice"));
        Assert.True(channel.IsInvited("ALICE"));
        Assert.True(channel.IsInvited("aLiCe"));
    }

    // 21. Key and UserLimit can be set
    [Fact]
    public void Key_And_UserLimit_CanBeSet()
    {
        var channel = new ChannelImpl("#test");

        Assert.Null(channel.Key);
        Assert.Null(channel.UserLimit);

        channel.Key = "secret";
        channel.UserLimit = 50;

        Assert.Equal("secret", channel.Key);
        Assert.Equal(50, channel.UserLimit);
    }

    // 22. CreatedAt is set on construction
    [Fact]
    public void CreatedAt_IsSetOnConstruction()
    {
        var before = DateTimeOffset.UtcNow;
        var channel = new ChannelImpl("#test");
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(channel.CreatedAt, before, after);
    }
}
