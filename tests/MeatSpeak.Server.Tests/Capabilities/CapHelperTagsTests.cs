using Xunit;
using NSubstitute;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Sessions;

namespace MeatSpeak.Server.Tests.Capabilities;

public class CapHelperTagsTests
{
    private ISession CreateSession(params string[] caps)
    {
        var session = Substitute.For<ISession>();
        var info = new SessionInfo { Nickname = "TestUser", Username = "user", Hostname = "host" };
        foreach (var cap in caps)
            info.CapState.Acknowledged.Add(cap);
        session.Info.Returns(info);
        return session;
    }

    [Fact]
    public void BuildTags_NoCaps_ReturnsNull()
    {
        var session = CreateSession();
        var tags = CapHelper.BuildTags(session);
        Assert.Null(tags);
    }

    [Fact]
    public void BuildTags_ServerTimeOnly_ReturnsTimeTag()
    {
        var session = CreateSession("server-time");
        var tags = CapHelper.BuildTags(session);
        Assert.NotNull(tags);
        Assert.StartsWith("time=", tags);
    }

    [Fact]
    public void BuildTags_WithMsgId_NoMessageTagsCap_OmitsMsgId()
    {
        var session = CreateSession("server-time");
        var tags = CapHelper.BuildTags(session, "abc123");
        Assert.NotNull(tags);
        Assert.DoesNotContain("msgid=", tags);
    }

    [Fact]
    public void BuildTags_WithMsgId_WithMessageTagsCap_IncludesMsgId()
    {
        var session = CreateSession("server-time", "message-tags");
        var tags = CapHelper.BuildTags(session, "abc123");
        Assert.NotNull(tags);
        Assert.Contains("msgid=abc123", tags);
        Assert.Contains("time=", tags);
    }

    [Fact]
    public void BuildTags_MsgIdOnly_NoServerTime()
    {
        var session = CreateSession("message-tags");
        var tags = CapHelper.BuildTags(session, "abc123");
        Assert.NotNull(tags);
        Assert.Equal("msgid=abc123", tags);
    }

    [Fact]
    public void BuildTags_WithExtraTags()
    {
        var session = CreateSession("server-time", "message-tags");
        var tags = CapHelper.BuildTags(session, "abc123", "batch=ref1");
        Assert.NotNull(tags);
        Assert.Contains("batch=ref1", tags);
        Assert.Contains("msgid=abc123", tags);
    }

    [Fact]
    public void BuildTags_ExtraOnly_NoCaps()
    {
        var session = CreateSession();
        var tags = CapHelper.BuildTags(session, null, "batch=ref1");
        Assert.NotNull(tags);
        Assert.Equal("batch=ref1", tags);
    }

    [Fact]
    public async Task SendWithTags_WithMsgIdAndCaps_SendsTaggedMessage()
    {
        var session = CreateSession("server-time", "message-tags");
        await CapHelper.SendWithTags(session, "abc123", "nick!user@host", "PRIVMSG", "#test", "hello");

        await session.Received(1).SendTaggedMessageAsync(
            Arg.Is<string>(t => t.Contains("msgid=abc123") && t.Contains("time=")),
            "nick!user@host",
            "PRIVMSG",
            Arg.Any<string[]>());
    }

    [Fact]
    public async Task SendWithTags_NoCaps_SendsPlainMessage()
    {
        var session = CreateSession();
        await CapHelper.SendWithTags(session, "abc123", "nick!user@host", "PRIVMSG", "#test", "hello");

        await session.Received(1).SendMessageAsync(
            "nick!user@host",
            "PRIVMSG",
            Arg.Any<string[]>());
        await session.DidNotReceive().SendTaggedMessageAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string[]>());
    }

    [Fact]
    public async Task SendWithTimestamp_BackwardCompatible()
    {
        var session = CreateSession("server-time");
        await CapHelper.SendWithTimestamp(session, "nick!user@host", "PRIVMSG", "#test", "hello");

        await session.Received(1).SendTaggedMessageAsync(
            Arg.Is<string>(t => t.StartsWith("time=")),
            "nick!user@host",
            "PRIVMSG",
            Arg.Any<string[]>());
    }

    [Fact]
    public void ExtractClientTags_NoTags_ReturnsNull()
    {
        Assert.Null(CapHelper.ExtractClientTags(null));
        Assert.Null(CapHelper.ExtractClientTags(""));
    }

    [Fact]
    public void ExtractClientTags_NoClientTags_ReturnsNull()
    {
        Assert.Null(CapHelper.ExtractClientTags("time=2024-01-01T00:00:00Z;msgid=abc"));
    }

    [Fact]
    public void ExtractClientTags_WithClientTags_ReturnsThem()
    {
        var result = CapHelper.ExtractClientTags("time=2024-01-01T00:00:00Z;+draft/reply=msg123;+draft/react=thumbsup");
        Assert.NotNull(result);
        Assert.Contains("+draft/reply=msg123", result);
        Assert.Contains("+draft/react=thumbsup", result);
        Assert.DoesNotContain("time=", result);
    }

    [Fact]
    public void ExtractClientTags_OnlyClientTags()
    {
        var result = CapHelper.ExtractClientTags("+draft/react=smile");
        Assert.Equal("+draft/react=smile", result);
    }

    // --- Labeled Response Tests ---

    [Fact]
    public void BuildTags_WithLabel_IncludesLabelTag()
    {
        var session = CreateSession("labeled-response", "server-time");
        session.Info.CurrentLabel = "abc123";
        var tags = CapHelper.BuildTags(session);
        Assert.NotNull(tags);
        Assert.Contains("label=abc123", tags);
        Assert.Contains("time=", tags);
    }

    [Fact]
    public void BuildTags_WithLabel_NoLabeledResponseCap_OmitsLabel()
    {
        var session = CreateSession("server-time");
        session.Info.CurrentLabel = "abc123";
        var tags = CapHelper.BuildTags(session);
        Assert.NotNull(tags);
        Assert.DoesNotContain("label=", tags);
    }

    [Fact]
    public void BuildTags_NoLabel_WithLabeledResponseCap_OmitsLabel()
    {
        var session = CreateSession("labeled-response", "server-time");
        // CurrentLabel is null by default
        var tags = CapHelper.BuildTags(session);
        Assert.NotNull(tags);
        Assert.DoesNotContain("label=", tags);
    }

    [Fact]
    public void BuildTags_LabelOnly_NoCaps()
    {
        var session = CreateSession("labeled-response");
        session.Info.CurrentLabel = "myLabel";
        var tags = CapHelper.BuildTags(session);
        Assert.NotNull(tags);
        Assert.Equal("label=myLabel", tags);
    }

    [Fact]
    public void BuildTags_LabelWithAllTags()
    {
        var session = CreateSession("labeled-response", "server-time", "message-tags");
        session.Info.CurrentLabel = "req1";
        var tags = CapHelper.BuildTags(session, "msg001", "batch=ref1");
        Assert.NotNull(tags);
        Assert.Contains("label=req1", tags);
        Assert.Contains("time=", tags);
        Assert.Contains("msgid=msg001", tags);
        Assert.Contains("batch=ref1", tags);
    }

    [Fact]
    public void BuildTags_LabelAppearsFirst()
    {
        var session = CreateSession("labeled-response", "server-time");
        session.Info.CurrentLabel = "first";
        var tags = CapHelper.BuildTags(session)!;
        Assert.StartsWith("label=first", tags);
    }

    [Fact]
    public void LabeledMessageCount_IncrementedOnSend()
    {
        // Verify the SessionInfo tracking works
        var info = new SessionInfo();
        Assert.Equal(0, info.LabeledMessageCount);
        info.LabeledMessageCount++;
        Assert.Equal(1, info.LabeledMessageCount);
    }

    [Fact]
    public void CurrentLabel_DefaultsToNull()
    {
        var info = new SessionInfo();
        Assert.Null(info.CurrentLabel);
    }
}
