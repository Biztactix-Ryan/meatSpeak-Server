using System.Text;
using Xunit;

namespace MeatSpeak.Protocol.Tests;

/// <summary>
/// Tests to validate buffer overflow protection in MessageBuilder and IrcLine
/// </summary>
public class BufferOverflowTests
{
    [Fact]
    public void MessageBuilder_Write_BufferTooSmall_ReturnsNegativeOne()
    {
        var buffer = new byte[5]; // Very small buffer

        int result = MessageBuilder.Write(buffer, null, "PING", "server.example.com");

        Assert.Equal(-1, result);
    }

    [Fact]
    public void MessageBuilder_Write_WithPrefix_BufferTooSmall_ReturnsNegativeOne()
    {
        var buffer = new byte[10]; // Small buffer

        int result = MessageBuilder.Write(buffer, "server.example.com", "PONG", "server.example.com");

        Assert.Equal(-1, result);
    }

    [Fact]
    public void MessageBuilder_Write_ExactSize_Succeeds()
    {
        // Calculate exact size needed: "PING server\r\n" = 13 bytes
        var buffer = new byte[13];

        int result = MessageBuilder.Write(buffer, null, "PING", "server");

        Assert.Equal(13, result);
        var output = Encoding.UTF8.GetString(buffer, 0, result);
        Assert.Equal("PING server\r\n", output);
    }

    [Fact]
    public void MessageBuilder_Write_OneByteTooSmall_ReturnsNegativeOne()
    {
        // One byte too small for "PING server\r\n"
        var buffer = new byte[12];

        int result = MessageBuilder.Write(buffer, null, "PING", "server");

        Assert.Equal(-1, result);
    }

    [Fact]
    public void MessageBuilder_WriteNumeric_BufferTooSmall_ReturnsNegativeOne()
    {
        var buffer = new byte[10]; // Small buffer

        int result = MessageBuilder.WriteNumeric(buffer, "server.example.com", 1, "nick", "Welcome to the server");

        Assert.Equal(-1, result);
    }

    [Fact]
    public void MessageBuilder_WriteNumeric_ExactSize_Succeeds()
    {
        // ":srv 001 n :hi\r\n" = 16 bytes
        var buffer = new byte[16];

        int result = MessageBuilder.WriteNumeric(buffer, "srv", 1, "n", "hi");

        Assert.Equal(16, result);
        var output = Encoding.UTF8.GetString(buffer, 0, result);
        Assert.Equal(":srv 001 n :hi\r\n", output);
    }

    [Fact]
    public void MessageBuilder_Write_VeryLongMessage_BufferTooSmall_ReturnsNegativeOne()
    {
        var buffer = new byte[50];
        var longParam = new string('x', 100); // 100 character parameter

        int result = MessageBuilder.Write(buffer, null, "PRIVMSG", "#channel", longParam);

        Assert.Equal(-1, result);
    }

    [Fact]
    public void MessageBuilder_Write_EmptyString_ZeroLength_Succeeds()
    {
        var buffer = new byte[512];

        int result = MessageBuilder.Write(buffer, null, "CMD", "");

        Assert.True(result > 0);
        var output = Encoding.UTF8.GetString(buffer, 0, result);
        Assert.Equal("CMD :\r\n", output);
    }

    [Fact]
    public void IrcLine_TryParse_EmptyInput_ReturnsFalse()
    {
        var result = IrcLine.TryParse(ReadOnlySpan<byte>.Empty, out _);

        Assert.False(result);
    }

    [Fact]
    public void IrcLine_TryParse_OnlyWhitespace_ReturnsFalse()
    {
        var bytes = Encoding.UTF8.GetBytes("\r\n");
        var result = IrcLine.TryParse(bytes, out _);

        Assert.False(result);
    }

    [Fact]
    public void IrcLine_TryParse_OnlyAtSign_ReturnsFalse()
    {
        var bytes = Encoding.UTF8.GetBytes("@");
        var result = IrcLine.TryParse(bytes, out _);

        Assert.False(result);
    }

    [Fact]
    public void IrcLine_TryParse_OnlyColon_ReturnsFalse()
    {
        var bytes = Encoding.UTF8.GetBytes(":");
        var result = IrcLine.TryParse(bytes, out _);

        Assert.False(result);
    }

    [Fact]
    public void IrcLine_TryParse_TagsWithoutCommand_ReturnsFalse()
    {
        var bytes = Encoding.UTF8.GetBytes("@tag=value");
        var result = IrcLine.TryParse(bytes, out _);

        Assert.False(result);
    }

    [Fact]
    public void IrcLine_TryParse_PrefixWithoutCommand_ReturnsFalse()
    {
        var bytes = Encoding.UTF8.GetBytes(":prefix");
        var result = IrcLine.TryParse(bytes, out _);

        Assert.False(result);
    }

    [Fact]
    public void IrcLine_TryParse_MalformedInputWithSingleByte_Succeeds()
    {
        var bytes = Encoding.UTF8.GetBytes("X");
        var result = IrcLine.TryParse(bytes, out var parts);

        Assert.True(result);
        Assert.Equal("X", Encoding.UTF8.GetString(parts.Command));
    }

    [Fact]
    public void IrcLine_TryParse_TrailingImmediatelyAfterCommand_Succeeds()
    {
        var bytes = Encoding.UTF8.GetBytes("CMD :trailing");
        var result = IrcLine.TryParse(bytes, out var parts);

        Assert.True(result);
        Assert.Equal("CMD", Encoding.UTF8.GetString(parts.Command));
        Assert.Equal("trailing", Encoding.UTF8.GetString(parts.Trailing));
        Assert.True(parts.HasTrailing);
    }

    [Fact]
    public void MessageBuilder_Write_MultipleParams_BufferTooSmall_ReturnsNegativeOne()
    {
        var buffer = new byte[20]; // Too small for multiple params

        int result = MessageBuilder.Write(buffer, "server", "PRIVMSG", "#channel", "param1", "param2", "Hello world");

        Assert.Equal(-1, result);
    }

    [Fact]
    public void MessageBuilder_Write_ZeroLengthBuffer_ReturnsNegativeOne()
    {
        var buffer = new byte[0];

        int result = MessageBuilder.Write(buffer, null, "PING", "server");

        Assert.Equal(-1, result);
    }

    [Fact]
    public void MessageBuilder_WriteNumeric_ZeroLengthBuffer_ReturnsNegativeOne()
    {
        var buffer = new byte[0];

        int result = MessageBuilder.WriteNumeric(buffer, "server", 1, "nick", "Welcome");

        Assert.Equal(-1, result);
    }
}
