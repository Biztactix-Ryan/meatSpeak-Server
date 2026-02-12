using Xunit;
using MeatSpeak.Server.Core.Channels;

namespace MeatSpeak.Server.Core.Tests;

public class ChannelMembershipTests
{
    [Fact]
    public void PrefixChar_Operator_ReturnsAt()
    {
        var m = new ChannelMembership { Nickname = "nick", IsOperator = true };
        Assert.Equal("@", m.PrefixChar);
    }

    [Fact]
    public void PrefixChar_Voice_ReturnsPlus()
    {
        var m = new ChannelMembership { Nickname = "nick", HasVoice = true };
        Assert.Equal("+", m.PrefixChar);
    }

    [Fact]
    public void PrefixChar_OperatorAndVoice_ReturnsAt()
    {
        var m = new ChannelMembership { Nickname = "nick", IsOperator = true, HasVoice = true };
        Assert.Equal("@", m.PrefixChar);
    }

    [Fact]
    public void PrefixChar_Regular_ReturnsEmpty()
    {
        var m = new ChannelMembership { Nickname = "nick" };
        Assert.Equal("", m.PrefixChar);
    }

    [Fact]
    public void AllPrefixChars_OperatorAndVoice_ReturnsBoth()
    {
        var m = new ChannelMembership { Nickname = "nick", IsOperator = true, HasVoice = true };
        Assert.Equal("@+", m.AllPrefixChars);
    }

    [Fact]
    public void AllPrefixChars_OperatorOnly_ReturnsAt()
    {
        var m = new ChannelMembership { Nickname = "nick", IsOperator = true };
        Assert.Equal("@", m.AllPrefixChars);
    }

    [Fact]
    public void AllPrefixChars_VoiceOnly_ReturnsPlus()
    {
        var m = new ChannelMembership { Nickname = "nick", HasVoice = true };
        Assert.Equal("+", m.AllPrefixChars);
    }

    [Fact]
    public void AllPrefixChars_Regular_ReturnsEmpty()
    {
        var m = new ChannelMembership { Nickname = "nick" };
        Assert.Equal("", m.AllPrefixChars);
    }
}
