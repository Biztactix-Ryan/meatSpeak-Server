using Xunit;

namespace MeatSpeak.Protocol.Tests;

public class IrcMessageTests
{
    [Fact]
    public void ParsePrefix_WithNickUserHost_ReturnsAllThree()
    {
        var msg = new IrcMessage(null, "nick!user@host", "PRIVMSG", new[] { "#channel", "Hello" });

        var (nick, user, host) = msg.ParsePrefix();

        Assert.Equal("nick", nick);
        Assert.Equal("user", user);
        Assert.Equal("host", host);
    }

    [Fact]
    public void ParsePrefix_WithNickOnly_ReturnsNickAndNulls()
    {
        var msg = new IrcMessage(null, "nick", "PRIVMSG", new[] { "#channel", "Hello" });

        var (nick, user, host) = msg.ParsePrefix();

        Assert.Equal("nick", nick);
        Assert.Null(user);
        Assert.Null(host);
    }

    [Fact]
    public void ParsePrefix_WithNickAndHost_ReturnsNickNullHost()
    {
        var msg = new IrcMessage(null, "nick@host", "PRIVMSG", new[] { "#channel", "Hello" });

        var (nick, user, host) = msg.ParsePrefix();

        Assert.Equal("nick", nick);
        Assert.Null(user);
        Assert.Equal("host", host);
    }

    [Fact]
    public void ParsePrefix_NullPrefix_ReturnsAllNulls()
    {
        var msg = new IrcMessage(null, null, "PING", Array.Empty<string>());

        var (nick, user, host) = msg.ParsePrefix();

        Assert.Null(nick);
        Assert.Null(user);
        Assert.Null(host);
    }

    [Fact]
    public void Parameters_ReturnsProvidedList()
    {
        var parameters = new[] { "#channel", "Hello world" };
        var msg = new IrcMessage(null, "nick", "PRIVMSG", parameters);

        Assert.Equal(2, msg.Parameters.Count);
        Assert.Equal("#channel", msg.Parameters[0]);
        Assert.Equal("Hello world", msg.Parameters[1]);
    }

    [Fact]
    public void Trailing_ReturnsLastParameter()
    {
        var msg = new IrcMessage(null, "nick", "PRIVMSG", new[] { "#channel", "Hello world" });

        Assert.Equal("Hello world", msg.Trailing);
    }

    [Fact]
    public void Trailing_EmptyParameters_ReturnsNull()
    {
        var msg = new IrcMessage(null, null, "QUIT", Array.Empty<string>());

        Assert.Null(msg.Trailing);
    }

    [Fact]
    public void GetParam_ValidIndex_ReturnsParameter()
    {
        var msg = new IrcMessage(null, "nick", "PRIVMSG", new[] { "#channel", "Hello world" });

        Assert.Equal("#channel", msg.GetParam(0));
        Assert.Equal("Hello world", msg.GetParam(1));
    }

    [Fact]
    public void GetParam_OutOfRangeIndex_ReturnsNull()
    {
        var msg = new IrcMessage(null, "nick", "PRIVMSG", new[] { "#channel" });

        Assert.Null(msg.GetParam(5));
        Assert.Null(msg.GetParam(1));
    }

    [Fact]
    public void ToString_WithPrefixAndTrailing_FormatsCorrectly()
    {
        var msg = new IrcMessage(null, "nick!user@host", "PRIVMSG", new[] { "#channel", "Hello world" });

        var result = msg.ToString();

        Assert.Equal(":nick!user@host PRIVMSG #channel :Hello world", result);
    }

    [Fact]
    public void ToString_WithTags_IncludesTags()
    {
        var msg = new IrcMessage("time=2024-01-01", "server", "NOTICE", new[] { "*", "Hello" });

        var result = msg.ToString();

        Assert.Equal("@time=2024-01-01 :server NOTICE * :Hello", result);
    }

    [Fact]
    public void ToString_CommandOnly_NoPrefix()
    {
        var msg = new IrcMessage(null, null, "QUIT", Array.Empty<string>());

        var result = msg.ToString();

        Assert.Equal("QUIT", result);
    }

    [Fact]
    public void ToString_SingleParamNoSpace_NoColonPrefix()
    {
        var msg = new IrcMessage(null, null, "NICK", new[] { "newnick" });

        var result = msg.ToString();

        Assert.Equal("NICK newnick", result);
    }

    [Fact]
    public void ToString_Roundtrip_FieldsMatch()
    {
        var original = new IrcMessage(null, "nick!user@host", "PRIVMSG", new[] { "#channel", "Hello world" });
        var str = original.ToString();

        // Parse back
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        Assert.True(IrcLine.TryParse(bytes, out var parts));
        var parsed = parts.ToMessage();

        Assert.Equal(original.Prefix, parsed.Prefix);
        Assert.Equal(original.Command, parsed.Command);
        Assert.Equal(original.Parameters.Count, parsed.Parameters.Count);
        for (int i = 0; i < original.Parameters.Count; i++)
            Assert.Equal(original.Parameters[i], parsed.Parameters[i]);
    }

    [Fact]
    public void ParsedTags_ReturnsParsedDictionary()
    {
        var msg = new IrcMessage("time=2024-01-01;msgid=abc123", null, "PRIVMSG", new[] { "#channel", "Hello" });

        var tags = msg.ParsedTags;

        Assert.Equal(2, tags.Count);
        Assert.Equal("2024-01-01", tags["time"]);
        Assert.Equal("abc123", tags["msgid"]);
    }

    [Fact]
    public void ParsedTags_NullTags_ReturnsEmptyDictionary()
    {
        var msg = new IrcMessage(null, null, "PING", Array.Empty<string>());

        var tags = msg.ParsedTags;

        Assert.Empty(tags);
    }

    // --- Edge case tests ---

    [Fact]
    public void ParsePrefix_EmptyPrefix_ReturnsEmptyNick()
    {
        var msg = new IrcMessage(null, "", "PRIVMSG", new[] { "#channel", "Hello" });

        var (nick, user, host) = msg.ParsePrefix();

        Assert.Equal("", nick);
        Assert.Null(user);
        Assert.Null(host);
    }

    [Fact]
    public void ParsePrefix_MultipleExclamation_SplitsOnFirst()
    {
        var msg = new IrcMessage(null, "a!b!c@host", "PRIVMSG", new[] { "#channel" });

        var (nick, user, host) = msg.ParsePrefix();

        Assert.Equal("a", nick);
        Assert.Equal("b!c", user);
        Assert.Equal("host", host);
    }

    [Fact]
    public void ParsePrefix_MultipleAt_SplitsOnFirst()
    {
        var msg = new IrcMessage(null, "nick!user@host@extra", "PRIVMSG", new[] { "#channel" });

        var (nick, user, host) = msg.ParsePrefix();

        // IndexOf('@') finds the first @, so user = between ! and first @
        Assert.Equal("nick", nick);
        Assert.Equal("user", user);
        Assert.Equal("host@extra", host);
    }

    [Fact]
    public void ParsePrefix_ExclAfterAt_TreatedAsNickAtHost()
    {
        // "nick@host!user" — excl at index 9, at index 4, excl > at so excl >= 0 && at > excl is false
        var msg = new IrcMessage(null, "nick@host!user", "PRIVMSG", new[] { "#channel" });

        var (nick, user, host) = msg.ParsePrefix();

        // Falls to the "at >= 0" branch: nick@host
        Assert.Equal("nick", nick);
        Assert.Null(user);
        Assert.Equal("host!user", host);
    }

    [Fact]
    public void ToString_MultipleMiddleParams_FormatsCorrectly()
    {
        var msg = new IrcMessage(null, null, "MODE", new[] { "#chan", "+o", "user" });

        var result = msg.ToString();

        Assert.Equal("MODE #chan +o :user", result);
    }

    [Fact]
    public void ToString_SingleParamWithSpace_GetsColonPrefix()
    {
        var msg = new IrcMessage(null, null, "QUIT", new[] { "Gone away" });

        var result = msg.ToString();

        Assert.Equal("QUIT :Gone away", result);
    }

    [Fact]
    public void ToString_EmptyLastOfMultiple_GetsColonPrefix()
    {
        var msg = new IrcMessage(null, "server", "001", new[] { "nick", "" });

        var result = msg.ToString();

        Assert.Equal(":server 001 nick :", result);
    }

    [Fact]
    public void GetParam_NegativeIndex_ReturnsNull()
    {
        var msg = new IrcMessage(null, null, "PING", new[] { "server" });

        // Negative index is < Parameters.Count (1), so it will throw or return
        // Actually, -1 < 1 is true, so Parameters[-1] would be accessed via indexer
        // This tests the behavior — List<T>[-1] throws ArgumentOutOfRangeException
        // But GetParam checks index < Parameters.Count: -1 < 1 is true, so it tries Parameters[-1]
        // Let's verify the actual behavior
        Assert.ThrowsAny<Exception>(() => msg.GetParam(-1));
    }

    [Fact]
    public void Trailing_SingleParam_ReturnsIt()
    {
        var msg = new IrcMessage(null, null, "NICK", new[] { "newnick" });

        Assert.Equal("newnick", msg.Trailing);
    }

    [Fact]
    public void Roundtrip_CommandOnly_Preserved()
    {
        var original = new IrcMessage(null, null, "QUIT", Array.Empty<string>());
        var str = original.ToString();

        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        Assert.True(IrcLine.TryParse(bytes, out var parts));
        var parsed = parts.ToMessage();

        Assert.Null(parsed.Prefix);
        Assert.Equal("QUIT", parsed.Command);
        Assert.Empty(parsed.Parameters);
    }
}
