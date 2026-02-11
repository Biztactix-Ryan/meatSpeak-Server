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
        // Last param has spaces, so colon is added
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
    public void WriteNumeric_LastParamWithoutSpaces_NoColonPrefix()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "server", 376, "nick", "EndOfMOTD");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":server 376 nick EndOfMOTD\r\n", output);
    }

    [Fact]
    public void WriteNumeric_LastParamWithSpaces_AddsColonPrefix()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "server", 372, "nick", "- Message of the day");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":server 372 nick :- Message of the day\r\n", output);
    }

    [Fact]
    public void WriteNumeric_EmptyLastParam_AddsColonPrefix()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "server", 331, "nick", "#channel", "");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":server 331 nick #channel :\r\n", output);
    }

    [Fact]
    public void WriteNumeric_LastParamStartsWithColon_AddsColonPrefix()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "server", 332, "nick", "#channel", ":Topic");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":server 332 nick #channel ::Topic\r\n", output);
    }

    [Fact]
    public void WriteNumeric_SingleWordParam_NoColonPrefix()
    {
        var buffer = new byte[512];

        int written = MessageBuilder.WriteNumeric(buffer, "server", 221, "nick", "+i");

        var output = Encoding.UTF8.GetString(buffer, 0, written);
        Assert.Equal(":server 221 nick +i\r\n", output);
    }
}
