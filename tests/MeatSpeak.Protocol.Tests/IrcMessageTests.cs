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
}
