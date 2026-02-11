using System.Text;
using MeatSpeak.Server.Transport.Tcp;
using Xunit;

namespace MeatSpeak.Server.Transport.Tests;

public class LineFramerTests
{
    [Fact]
    public void Scan_SingleCompleteLineWithCrLf_CallsCallbackAndReturnsConsumed()
    {
        var input = "PING server\r\n"u8.ToArray();
        var lines = new List<string>();

        int consumed = LineFramer.Scan(input, 0, input.Length, line =>
        {
            lines.Add(Encoding.UTF8.GetString(line));
        });

        Assert.Single(lines);
        Assert.Equal("PING server", lines[0]);
        Assert.Equal(input.Length, consumed);
    }

    [Fact]
    public void Scan_SingleCompleteLineWithLfOnly_CallsCallbackAndReturnsConsumed()
    {
        var input = "PING server\n"u8.ToArray();
        var lines = new List<string>();

        int consumed = LineFramer.Scan(input, 0, input.Length, line =>
        {
            lines.Add(Encoding.UTF8.GetString(line));
        });

        Assert.Single(lines);
        Assert.Equal("PING server", lines[0]);
        Assert.Equal(input.Length, consumed);
    }

    [Fact]
    public void Scan_MultipleLinesInOneBuffer_CallsCallbackForEach()
    {
        var input = "NICK foo\r\nUSER bar 0 * :Real Name\r\n"u8.ToArray();
        var lines = new List<string>();

        int consumed = LineFramer.Scan(input, 0, input.Length, line =>
        {
            lines.Add(Encoding.UTF8.GetString(line));
        });

        Assert.Equal(2, lines.Count);
        Assert.Equal("NICK foo", lines[0]);
        Assert.Equal("USER bar 0 * :Real Name", lines[1]);
        Assert.Equal(input.Length, consumed);
    }

    [Fact]
    public void Scan_PartialLineNoLineEnding_ReturnsZeroConsumed()
    {
        var input = "NICK foo"u8.ToArray();
        var lines = new List<string>();

        int consumed = LineFramer.Scan(input, 0, input.Length, line =>
        {
            lines.Add(Encoding.UTF8.GetString(line));
        });

        Assert.Empty(lines);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Scan_EmptyLineBetweenTwoLines_SkipsEmptyLine()
    {
        // Empty line between two valid lines (\r\n\r\n means an empty line)
        var input = "NICK foo\r\n\r\nUSER bar 0 * :Real\r\n"u8.ToArray();
        var lines = new List<string>();

        int consumed = LineFramer.Scan(input, 0, input.Length, line =>
        {
            lines.Add(Encoding.UTF8.GetString(line));
        });

        // The LineFramer skips empty lines (lineLength > 0 check)
        Assert.Equal(2, lines.Count);
        Assert.Equal("NICK foo", lines[0]);
        Assert.Equal("USER bar 0 * :Real", lines[1]);
        Assert.Equal(input.Length, consumed);
    }

    [Fact]
    public void Scan_LineFollowedByPartial_ReturnsOnlyCompleteLineConsumed()
    {
        var input = "PING server\r\nNICK partial"u8.ToArray();
        var lines = new List<string>();

        int consumed = LineFramer.Scan(input, 0, input.Length, line =>
        {
            lines.Add(Encoding.UTF8.GetString(line));
        });

        Assert.Single(lines);
        Assert.Equal("PING server", lines[0]);
        // consumed should be just the first complete line (including \r\n)
        Assert.Equal("PING server\r\n".Length, consumed);
    }

    [Fact]
    public void Scan_WithOffset_ScansFromOffset()
    {
        // Put garbage bytes before the real content
        var prefix = new byte[] { 0xFF, 0xFF, 0xFF };
        var lineBytes = "PING\r\n"u8.ToArray();
        var input = new byte[prefix.Length + lineBytes.Length];
        prefix.CopyTo(input, 0);
        lineBytes.CopyTo(input, prefix.Length);

        var lines = new List<string>();

        int consumed = LineFramer.Scan(input, prefix.Length, lineBytes.Length, line =>
        {
            lines.Add(Encoding.UTF8.GetString(line));
        });

        Assert.Single(lines);
        Assert.Equal("PING", lines[0]);
        Assert.Equal(lineBytes.Length, consumed);
    }

    [Fact]
    public void Scan_EmptyBuffer_ReturnsZero()
    {
        var input = Array.Empty<byte>();
        var lines = new List<string>();

        int consumed = LineFramer.Scan(input, 0, 0, line =>
        {
            lines.Add(Encoding.UTF8.GetString(line));
        });

        Assert.Empty(lines);
        Assert.Equal(0, consumed);
    }

    [Fact]
    public void Scan_MixedLineEndings_HandlesCorrectly()
    {
        // First line ends with \n only, second with \r\n
        var input = "PING\nPONG server\r\n"u8.ToArray();
        var lines = new List<string>();

        int consumed = LineFramer.Scan(input, 0, input.Length, line =>
        {
            lines.Add(Encoding.UTF8.GetString(line));
        });

        Assert.Equal(2, lines.Count);
        Assert.Equal("PING", lines[0]);
        Assert.Equal("PONG server", lines[1]);
        Assert.Equal(input.Length, consumed);
    }
}
