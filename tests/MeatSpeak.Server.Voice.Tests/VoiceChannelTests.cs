using Xunit;

namespace MeatSpeak.Server.Voice.Tests;

public class VoiceChannelTests
{
    // ─── Constructor ───

    [Fact]
    public void Constructor_SetsName()
    {
        var channel = new VoiceChannel("#voice");

        Assert.Equal("#voice", channel.Name);
    }

    // ─── AddMember ───

    [Fact]
    public void AddMember_Succeeds()
    {
        var channel = new VoiceChannel("#voice");
        var session = new VoiceSession("session1", 1);

        Assert.True(channel.AddMember(session));
    }

    [Fact]
    public void AddMember_DuplicateReturnsFalse()
    {
        var channel = new VoiceChannel("#voice");
        var session = new VoiceSession("session1", 1);

        channel.AddMember(session);

        Assert.False(channel.AddMember(session));
    }

    // ─── RemoveMember ───

    [Fact]
    public void RemoveMember_Succeeds()
    {
        var channel = new VoiceChannel("#voice");
        var session = new VoiceSession("session1", 1);
        channel.AddMember(session);

        Assert.True(channel.RemoveMember("session1"));
    }

    [Fact]
    public void RemoveMember_UnknownReturnsFalse()
    {
        var channel = new VoiceChannel("#voice");

        Assert.False(channel.RemoveMember("nonexistent"));
    }

    // ─── Members dictionary ───

    [Fact]
    public void Members_ReflectsChanges()
    {
        var channel = new VoiceChannel("#voice");
        var session1 = new VoiceSession("session1", 1);
        var session2 = new VoiceSession("session2", 2);

        Assert.Empty(channel.Members);

        channel.AddMember(session1);
        Assert.Single(channel.Members);

        channel.AddMember(session2);
        Assert.Equal(2, channel.Members.Count);

        channel.RemoveMember("session1");
        Assert.Single(channel.Members);
        Assert.True(channel.Members.ContainsKey("session2"));
    }

    // ─── Properties ───

    [Fact]
    public void PrioritySpeakerAndGroupKey_CanBeSet()
    {
        var channel = new VoiceChannel("#voice");

        Assert.Null(channel.PrioritySpeaker);
        Assert.Null(channel.GroupKey);

        channel.PrioritySpeaker = "session1";
        channel.GroupKey = new byte[] { 0x01, 0x02, 0x03 };

        Assert.Equal("session1", channel.PrioritySpeaker);
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, channel.GroupKey);
    }
}

public class VoiceSessionTests
{
    [Fact]
    public void Constructor_SetsProperties()
    {
        var session = new VoiceSession("session1", 42);

        Assert.Equal("session1", session.SessionId);
        Assert.Equal(42u, session.Ssrc);
    }

    [Fact]
    public void MuteDeafenFlags_DefaultToFalse()
    {
        var session = new VoiceSession("session1", 1);

        Assert.False(session.IsMuted);
        Assert.False(session.IsDeafened);
        Assert.False(session.IsSelfMuted);
        Assert.False(session.IsSelfDeafened);
    }
}
