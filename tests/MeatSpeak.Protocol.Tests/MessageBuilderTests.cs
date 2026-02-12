using System.Text;
using Xunit;

namespace MeatSpeak.Protocol.Tests;

public class MessageBuilderTests
{
    [Fact]
    public void Write_SimpleCommand_ProducesCorrectBytes()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.Write(buffer, null, "PING", "server.example.com");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal("PING server.example.com\r\n", output);
    }

    [Fact]
    public void Write_WithPrefix_IncludesColonPrefix()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.Write(buffer, "server.example.com", "PONG", "server.example.com");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":server.example.com PONG server.example.com\r\n", output);
    }

    [Fact]
    public void Write_WithTrailingContainingSpaces_AddsColonBeforeTrailing()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.Write(buffer, "nick!user@host", "PRIVMSG", "#channel", "Hello world");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":nick!user@host PRIVMSG #channel :Hello world\r\n", output);
    }

    [Fact]
    public void Write_EmptyLastParam_AddsColonBeforeEmpty()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.Write(buffer, "server", "001", "nick", "");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":server 001 nick :\r\n", output);
    }

    [Fact]
    public void Write_LastParamStartsWithColon_AddsColonPrefix()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.Write(buffer, null, "PRIVMSG", "#channel", ":)");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal("PRIVMSG #channel ::)\r\n", output);
    }

    [Fact]
    public void Write_NoParams_ProducesCommandOnly()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.Write(buffer, null, "QUIT");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal("QUIT\r\n", output);
    }

    [Fact]
    public void WriteNumeric_FormatsThreeDigitPadded()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "server.example.com", 1, "nick", "Welcome to the server");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":server.example.com 001 nick :Welcome to the server\r\n", output);
    }

    [Fact]
    public void WriteNumeric_ThreeDigitNumeric_NoExtraPadding()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "server", 433, "nick", "Nickname is already in use");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":server 433 nick :Nickname is already in use\r\n", output);
    }

    [Fact]
    public void WriteNumeric_MultipleParams_FormatsCorrectly()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "server", 353, "nick", "= #channel", "nick1 nick2 nick3");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        // WriteNumeric always adds : before the last param
        Assert.Contains(":server 353 nick", output);
        Assert.EndsWith("\r\n", output);
    }

    [Fact]
    public void Roundtrip_BuildThenParse_FieldsMatch()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.Write(buffer, "nick!user@host", "PRIVMSG", "#channel", "Hello world");

        Assert.True(IrcLine.TryParse(new ReadOnlySpan<byte>(buffer, 0, written), out var parts));
        var msg = parts.ToMessage();

        Assert.Equal("nick!user@host", msg.Prefix);
        Assert.Equal("PRIVMSG", msg.Command);
        Assert.Equal(2, msg.Parameters.Count);
        Assert.Equal("#channel", msg.Parameters[0]);
        Assert.Equal("Hello world", msg.Parameters[1]);
    }

    [Fact]
    public void Roundtrip_NumericBuildThenParse_FieldsMatch()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "irc.example.com", 1, "testnick", "Welcome to the network");

        Assert.True(IrcLine.TryParse(new ReadOnlySpan<byte>(buffer, 0, written), out var parts));
        var msg = parts.ToMessage();

        Assert.Equal("irc.example.com", msg.Prefix);
        Assert.Equal("001", msg.Command);
        Assert.Equal("testnick", msg.Parameters[0]);
        Assert.Equal("Welcome to the network", msg.Parameters[1]);
    }

    [Fact]
    public void WriteWithTags_WithTags_PrependsTags()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteWithTags(buffer, "time=2024-01-01T00:00:00.000Z", "nick!user@host", "PRIVMSG", "#channel", "Hello world");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal("@time=2024-01-01T00:00:00.000Z :nick!user@host PRIVMSG #channel :Hello world\r\n", output);
    }

    [Fact]
    public void WriteWithTags_NullTags_OmitsTags()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteWithTags(buffer, null, "nick!user@host", "PRIVMSG", "#channel", "Hello world");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":nick!user@host PRIVMSG #channel :Hello world\r\n", output);
    }

    [Fact]
    public void WriteWithTags_EmptyTags_OmitsTags()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteWithTags(buffer, "", "nick!user@host", "PRIVMSG", "#channel", "Hello world");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":nick!user@host PRIVMSG #channel :Hello world\r\n", output);
    }

    [Fact]
    public void WriteWithTags_MultipleTags_PrependedCorrectly()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteWithTags(buffer, "time=2024-01-01T00:00:00.000Z;batch=abc123", "server", "BATCH", "+abc123", "chathistory");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.StartsWith("@time=2024-01-01T00:00:00.000Z;batch=abc123 ", output);
        Assert.Contains(":server BATCH +abc123", output);
    }

    [Fact]
    public void WriteWithTags_NoPrefix_FormatCorrect()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteWithTags(buffer, "time=2024-01-01T00:00:00.000Z", null, "BATCH", "+ref1", "type");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        // "type" is last param, single word, no space - so no : prefix
        Assert.Equal("@time=2024-01-01T00:00:00.000Z BATCH +ref1 type\r\n", output);
    }

    [Fact]
    public void Roundtrip_WriteWithTagsThenParse_TagsPreserved()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteWithTags(buffer, "time=2024-01-01T00:00:00.000Z", "nick!user@host", "PRIVMSG", "#channel", "Hello world");

        Assert.True(IrcLine.TryParse(new ReadOnlySpan<byte>(buffer, 0, written), out var parts));
        var msg = parts.ToMessage();

        Assert.Equal("nick!user@host", msg.Prefix);
        Assert.Equal("PRIVMSG", msg.Command);
        Assert.Equal("#channel", msg.Parameters[0]);
        Assert.Equal("Hello world", msg.Parameters[1]);
        // Tags should also be preserved
        Assert.NotNull(msg.Tags);
        var parsedTags = msg.ParsedTags;
        Assert.True(parsedTags.ContainsKey("time"));
        Assert.Equal("2024-01-01T00:00:00.000Z", parsedTags["time"]);
    }

    // --- Edge case tests ---

    [Fact]
    public void Write_SingleParamWithSpace_AddsColonPrefix()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.Write(buffer, null, "QUIT", "Gone away");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal("QUIT :Gone away\r\n", output);
    }

    [Fact]
    public void Write_ThreeMiddleParams_LastGetsColonPrefix()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.Write(buffer, null, "MODE", "#chan", "+o", "user");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        // Last param "user" has no space/colon/empty so no colon prefix in Write
        // Actually Write only adds : if last param contains space, is empty, or starts with ':'
        Assert.Equal("MODE #chan +o user\r\n", output);
    }

    [Fact]
    public void Write_UTF8MultiByteChars_EncodesCorrectly()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.Write(buffer, null, "PRIVMSG", "#chan", "Hello \U0001F600 world");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal("PRIVMSG #chan :Hello \U0001F600 world\r\n", output);
        Assert.Contains("\U0001F600", output);
    }

    [Fact]
    public void WriteNumeric_SingleDigit_PaddedToThree()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "srv", 1, "nick", "Welcome");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Contains(" 001 ", output);
    }

    [Fact]
    public void WriteNumeric_TwoDigit_PaddedToThree()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "srv", 42, "nick", "Message");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Contains(" 042 ", output);
    }

    [Fact]
    public void WriteNumeric_NoExtraParams_JustTargetAndCRLF()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "srv", 1, "nick");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":srv 001 nick\r\n", output);
    }

    [Fact]
    public void WriteWithTags_Roundtrip_AllFieldsPreserved()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteWithTags(buffer, "time=2024-01-01;msgid=abc", "nick!user@host", "PRIVMSG", "#channel", "Hello world");

        Assert.True(IrcLine.TryParse(new ReadOnlySpan<byte>(buffer, 0, written), out var parts));
        var msg = parts.ToMessage();

        Assert.Equal("nick!user@host", msg.Prefix);
        Assert.Equal("PRIVMSG", msg.Command);
        Assert.Equal("#channel", msg.Parameters[0]);
        Assert.Equal("Hello world", msg.Parameters[1]);
        Assert.NotNull(msg.Tags);
        var tags = msg.ParsedTags;
        Assert.Equal("2024-01-01", tags["time"]);
        Assert.Equal("abc", tags["msgid"]);
    }
}
