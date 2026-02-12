using System.Text;
using Xunit;

namespace MeatSpeak.Protocol.Tests;

public class IrcLineTests
{
    private static bool Parse(string input, out IrcLineParts parts)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        return IrcLine.TryParse(bytes, out parts);
    }

    [Fact]
    public void SimpleCommand_ParsesCommandOnly()
    {
        var result = Parse("PING", out var parts);

        Assert.True(result);
        Assert.Equal("PING", Encoding.UTF8.GetString(parts.Command));
        Assert.True(parts.Prefix.IsEmpty);
        Assert.True(parts.Tags.IsEmpty);
        Assert.True(parts.ParamsRaw.IsEmpty);
        Assert.False(parts.HasTrailing);
    }

    [Fact]
    public void CommandWithParams_ParsesCommandAndTrailing()
    {
        var result = Parse("PRIVMSG #channel :Hello world", out var parts);

        Assert.True(result);
        Assert.Equal("PRIVMSG", Encoding.UTF8.GetString(parts.Command));
        Assert.Equal("#channel", Encoding.UTF8.GetString(parts.ParamsRaw));
        Assert.Equal("Hello world", Encoding.UTF8.GetString(parts.Trailing));
        Assert.True(parts.HasTrailing);
    }

    [Fact]
    public void FullMessage_ParsesPrefixCommandAndParams()
    {
        var result = Parse(":nick!user@host PRIVMSG #channel :Hello world", out var parts);

        Assert.True(result);
        Assert.Equal("nick!user@host", Encoding.UTF8.GetString(parts.Prefix));
        Assert.Equal("PRIVMSG", Encoding.UTF8.GetString(parts.Command));
        Assert.Equal("#channel", Encoding.UTF8.GetString(parts.ParamsRaw));
        Assert.Equal("Hello world", Encoding.UTF8.GetString(parts.Trailing));
        Assert.True(parts.HasTrailing);
    }

    [Fact]
    public void WithTags_ParsesTagsPrefixCommandAndParams()
    {
        var result = Parse("@time=2024-01-01T00:00:00Z :nick!user@host PRIVMSG #channel :Hello world", out var parts);

        Assert.True(result);
        Assert.Equal("time=2024-01-01T00:00:00Z", Encoding.UTF8.GetString(parts.Tags));
        Assert.Equal("nick!user@host", Encoding.UTF8.GetString(parts.Prefix));
        Assert.Equal("PRIVMSG", Encoding.UTF8.GetString(parts.Command));
        Assert.Equal("#channel", Encoding.UTF8.GetString(parts.ParamsRaw));
        Assert.Equal("Hello world", Encoding.UTF8.GetString(parts.Trailing));
        Assert.True(parts.HasTrailing);
    }

    [Fact]
    public void EmptyTrailing_ParsesWithEmptyTrailingString()
    {
        var result = Parse(":server 001 nick :", out var parts);

        Assert.True(result);
        Assert.Equal("server", Encoding.UTF8.GetString(parts.Prefix));
        Assert.Equal("001", Encoding.UTF8.GetString(parts.Command));
        Assert.Equal("nick", Encoding.UTF8.GetString(parts.ParamsRaw));
        Assert.True(parts.HasTrailing);
        Assert.True(parts.Trailing.IsEmpty);
    }

    [Fact]
    public void NoTrailing_ParsesSingleParam()
    {
        var result = Parse("NICK newnick", out var parts);

        Assert.True(result);
        Assert.Equal("NICK", Encoding.UTF8.GetString(parts.Command));
        Assert.Equal("newnick", Encoding.UTF8.GetString(parts.ParamsRaw));
        Assert.False(parts.HasTrailing);
    }

    [Fact]
    public void MultipleParamsWithTrailing_ParsesCorrectly()
    {
        // USER username 0 * :realname
        var result = Parse("USER username 0 * :realname", out var parts);

        Assert.True(result);
        Assert.Equal("USER", Encoding.UTF8.GetString(parts.Command));
        Assert.Equal("username 0 *", Encoding.UTF8.GetString(parts.ParamsRaw));
        Assert.Equal("realname", Encoding.UTF8.GetString(parts.Trailing));
        Assert.True(parts.HasTrailing);
    }

    [Fact]
    public void EmptySpan_ReturnsFalse()
    {
        var result = IrcLine.TryParse(ReadOnlySpan<byte>.Empty, out _);

        Assert.False(result);
    }

    [Fact]
    public void StripsCarriageReturnLineFeed()
    {
        var result = Parse("PING\r\n", out var parts);

        Assert.True(result);
        Assert.Equal("PING", Encoding.UTF8.GetString(parts.Command));
    }

    [Fact]
    public void StripsLineFeedOnly()
    {
        var result = Parse("PING\n", out var parts);

        Assert.True(result);
        Assert.Equal("PING", Encoding.UTF8.GetString(parts.Command));
    }

    [Fact]
    public void OnlyCrLf_ReturnsFalse()
    {
        var result = Parse("\r\n", out _);

        Assert.False(result);
    }

    [Fact]
    public void ToMessage_ConvertsCorrectly()
    {
        Parse(":nick!user@host PRIVMSG #channel :Hello world", out var parts);

        var msg = parts.ToMessage();

        Assert.Equal("nick!user@host", msg.Prefix);
        Assert.Equal("PRIVMSG", msg.Command);
        Assert.Equal(2, msg.Parameters.Count);
        Assert.Equal("#channel", msg.Parameters[0]);
        Assert.Equal("Hello world", msg.Parameters[1]);
    }

    [Fact]
    public void ToMessage_WithTags_PreservesTags()
    {
        Parse("@time=2024-01-01T00:00:00Z :server NOTICE * :Hello", out var parts);

        var msg = parts.ToMessage();

        Assert.Equal("time=2024-01-01T00:00:00Z", msg.Tags);
        Assert.Equal("server", msg.Prefix);
        Assert.Equal("NOTICE", msg.Command);
        Assert.Equal("*", msg.Parameters[0]);
        Assert.Equal("Hello", msg.Parameters[1]);
    }

    [Fact]
    public void ToMessage_CommandOnly_HasNoParameters()
    {
        Parse("PING", out var parts);

        var msg = parts.ToMessage();

        Assert.Null(msg.Tags);
        Assert.Null(msg.Prefix);
        Assert.Equal("PING", msg.Command);
        Assert.Empty(msg.Parameters);
    }

    // --- Edge case tests ---

    [Fact]
    public void TagsOnly_NoCommand_ReturnsFalse()
    {
        // "@tag=value" with no space after tags → TryParse returns false
        var result = Parse("@tag=value", out _);

        Assert.False(result);
    }

    [Fact]
    public void PrefixOnly_NoCommand_ReturnsFalse()
    {
        // ":server" with no space → TryParse returns false
        var result = Parse(":server", out _);

        Assert.False(result);
    }

    [Fact]
    public void TagsAndPrefix_NoCommand_ReturnsFalse()
    {
        // Tags and prefix present but remaining is empty after stripping spaces
        var result = Parse("@t=v :srv   ", out _);

        Assert.False(result);
    }

    [Fact]
    public void MultipleSpacesAfterPrefix_ParsesCommand()
    {
        var result = Parse(":server  PING", out var parts);

        Assert.True(result);
        Assert.Equal("server", Encoding.UTF8.GetString(parts.Prefix));
        Assert.Equal("PING", Encoding.UTF8.GetString(parts.Command));
    }

    [Fact]
    public void MultipleSpacesAfterCommand_ParsesParams()
    {
        var result = Parse("PRIVMSG  #chan :hi", out var parts);

        Assert.True(result);
        Assert.Equal("PRIVMSG", Encoding.UTF8.GetString(parts.Command));
        Assert.Equal("#chan", Encoding.UTF8.GetString(parts.ParamsRaw));
        Assert.Equal("hi", Encoding.UTF8.GetString(parts.Trailing));
        Assert.True(parts.HasTrailing);
    }

    [Fact]
    public void TrailingStartsImmediatelyAfterCommand()
    {
        // "PRIVMSG :hello" — no middle params, trailing only
        var result = Parse("PRIVMSG :hello", out var parts);

        Assert.True(result);
        Assert.Equal("PRIVMSG", Encoding.UTF8.GetString(parts.Command));
        Assert.True(parts.ParamsRaw.IsEmpty);
        Assert.Equal("hello", Encoding.UTF8.GetString(parts.Trailing));
        Assert.True(parts.HasTrailing);
    }

    [Fact]
    public void MultipleMiddleParams_ParsesAllViaToMessage()
    {
        Parse("MODE #chan +o user", out var parts);

        var msg = parts.ToMessage();

        Assert.Equal("MODE", msg.Command);
        Assert.Equal(3, msg.Parameters.Count);
        Assert.Equal("#chan", msg.Parameters[0]);
        Assert.Equal("+o", msg.Parameters[1]);
        Assert.Equal("user", msg.Parameters[2]);
    }

    [Fact]
    public void ColonInMiddleOfParam_NotTreatedAsTrailing()
    {
        // "PRIVMSG #chan hey:there" — colon not preceded by space, not trailing
        var result = Parse("PRIVMSG #chan hey:there", out var parts);

        Assert.True(result);
        Assert.False(parts.HasTrailing);
        var msg = parts.ToMessage();
        Assert.Equal(2, msg.Parameters.Count);
        Assert.Equal("#chan", msg.Parameters[0]);
        Assert.Equal("hey:there", msg.Parameters[1]);
    }

    [Fact]
    public void OnlySpaces_ParsesWithEmptyCommand()
    {
        // All spaces: parser finds space at index 0, yielding an empty command
        var result = Parse("   ", out var parts);

        Assert.True(result);
        Assert.True(parts.Command.IsEmpty);
    }

    [Fact]
    public void OnlyCR_ReturnsFalse()
    {
        var result = Parse("\r", out _);

        Assert.False(result);
    }

    [Fact]
    public void UTF8InTrailing_PreservesEncoding()
    {
        var result = Parse("PRIVMSG #chan :\u00e9\u00e0\u00fc \U0001F600", out var parts);

        Assert.True(result);
        Assert.True(parts.HasTrailing);
        var trailing = Encoding.UTF8.GetString(parts.Trailing);
        Assert.Contains("\u00e9", trailing);
        Assert.Contains("\U0001F600", trailing);
    }

    [Fact]
    public void SingleCharCommand_Succeeds()
    {
        var result = Parse("A", out var parts);

        Assert.True(result);
        Assert.Equal("A", Encoding.UTF8.GetString(parts.Command));
    }
}
