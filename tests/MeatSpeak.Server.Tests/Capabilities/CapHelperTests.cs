using Xunit;
using NSubstitute;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Sessions;

namespace MeatSpeak.Server.Tests.Capabilities;

public class CapHelperTests
{
    private ISession CreateSession(params string[] caps)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = "Test", Username = "user", Hostname = "host" };
        foreach (var cap in caps)
            info.CapState.Acknowledged.Add(cap);
        session.Info.Returns(info);
        session.Id.Returns("test-id");
        return session;
    }

    [Fact]
    public void HasCap_WithCap_ReturnsTrue()
    {
        var session = CreateSession("server-time");
        Assert.True(CapHelper.HasCap(session, "server-time"));
    }

    [Fact]
    public void HasCap_WithoutCap_ReturnsFalse()
    {
        var session = CreateSession();
        Assert.False(CapHelper.HasCap(session, "server-time"));
    }

    [Fact]
    public void HasCap_DifferentCap_ReturnsFalse()
    {
        var session = CreateSession("echo-message");
        Assert.False(CapHelper.HasCap(session, "server-time"));
    }

    [Fact]
    public void TimeTag_ReturnsValidFormat()
    {
        var tag = CapHelper.TimeTag();

        Assert.StartsWith("time=", tag);
        // Should be ISO 8601 UTC: time=2024-01-01T00:00:00.000Z
        var timeStr = tag["time=".Length..];
        Assert.True(DateTimeOffset.TryParse(timeStr, out var parsed));
        Assert.Equal(TimeSpan.Zero, parsed.Offset); // UTC
    }

    [Fact]
    public async Task SendWithTimestamp_WithServerTimeCap_SendsTagged()
    {
        var session = CreateSession("server-time");

        await CapHelper.SendWithTimestamp(session, "nick!user@host", "PRIVMSG", "#test", "hello");

        await session.Received().SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            "nick!user@host", "PRIVMSG",
            Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "hello"));
    }

    [Fact]
    public async Task SendWithTimestamp_WithoutServerTimeCap_SendsRegular()
    {
        var session = CreateSession();

        await CapHelper.SendWithTimestamp(session, "nick!user@host", "PRIVMSG", "#test", "hello");

        await session.Received().SendMessageAsync(
            "nick!user@host", "PRIVMSG",
            Arg.Is<string[]>(p => p[0] == "#test" && p[1] == "hello"));
    }

    [Fact]
    public async Task SendWithTimestamp_NoParams_Works()
    {
        var session = CreateSession();

        await CapHelper.SendWithTimestamp(session, "nick!user@host", "AWAY");

        await session.Received().SendMessageAsync(
            "nick!user@host", "AWAY",
            Arg.Is<string[]>(p => p.Length == 0));
    }
}
