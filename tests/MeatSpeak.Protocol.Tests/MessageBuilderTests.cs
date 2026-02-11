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
    public void Constants_MaxMessageLength_IsCorrectForRFC1459()
    {
        // RFC 1459: Max line length is 512 bytes INCLUDING CR/LF
        // Therefore max message content is 510 bytes (512 - 2)
        Assert.Equal(512, IrcConstants.MaxLineLength);
        Assert.Equal(510, IrcConstants.MaxMessageLength);
        Assert.Equal(IrcConstants.MaxLineLength - 2, IrcConstants.MaxMessageLength);
    }

    [Fact]
    public void Write_ShortMessage_IncludesCrLfInOutput()
    {
        // Verify that CR/LF are always added to messages
        var buffer = new byte[512];
        int written = MessageBuilder.Write(buffer, "prefix", "CMD", "param");

        // Should end with CR/LF
        Assert.Equal(IrcConstants.CR, buffer[written - 2]);
        Assert.Equal(IrcConstants.LF, buffer[written - 1]);
    }

    [Fact]
    public void Write_OversizedMessage_DoesNotEnforceLimit()
    {
        // This test documents that MessageBuilder does NOT enforce the 512-byte limit.
        // Callers are responsible for ensuring messages do not exceed MaxMessageLength (510 bytes)
        // before the CR/LF terminator.
        // 
        // Per RFC 1459: "IRC messages are always lines of characters terminated with a CR-LF
        // (Carriage Return - Line Feed) pair, and these messages SHALL NOT exceed 512 characters
        // in length, counting all characters including the trailing CR-LF."
        
        var buffer = new byte[1024];
        var longMessage = new string('A', 500);
        int written = MessageBuilder.Write(buffer, "short", "CMD", longMessage);

        // This will exceed 512 bytes - documenting the current behavior
        // Format is: :short CMD :AAAA...\r\n
        // = 1 + 5 + 1 + 3 + 1 + 1 + 500 + 2 = 514 bytes
        Assert.True(written > IrcConstants.MaxLineLength,
            "Oversized messages currently exceed RFC limit - callers must validate length");
    }

    [Fact]
    public void Write_CallerTruncatesToMaxMessageLength_ProducesRFC1459CompliantLine()
    {
        // This test demonstrates the EXPECTED caller behavior: truncating message content
        // to fit within MaxMessageLength before calling Write.
        
        var buffer = new byte[1024];
        
        // Caller should calculate the available space for content
        var prefix = "nick!user@host";
        var command = "PRIVMSG";
        var target = "#channel";
        // Format will be: :prefix COMMAND target :message\r\n
        // Calculate overhead: : prefix SPACE command SPACE target SPACE : \r\n
        var overhead = 1 + prefix.Length + 1 + command.Length + 1 + target.Length + 1 + 1 + 2;
        var maxContentLength = IrcConstants.MaxMessageLength - overhead;
        
        // Caller truncates their message to fit
        var originalMessage = new string('X', 1000); // Very long message
        var truncatedMessage = originalMessage[..maxContentLength];
        
        int written = MessageBuilder.Write(buffer, prefix, command, target, truncatedMessage);
        
        // Result should be RFC 1459 compliant (≤ 512 bytes)
        Assert.True(written <= IrcConstants.MaxLineLength,
            $"Message length {written} should not exceed {IrcConstants.MaxLineLength} when caller truncates properly");
        Assert.Equal(IrcConstants.CR, buffer[written - 2]);
        Assert.Equal(IrcConstants.LF, buffer[written - 1]);
    }
}
