using MeatSpeak.Server.Core.Modes;
using Xunit;

namespace MeatSpeak.Server.Core.Tests;

public class ModeRegistryTests
{
    [Fact]
    public void RegisterChannelMode_And_ResolveChannelMode_Works()
    {
        var registry = new ModeRegistry();
        var mode = new ModeDefinition('i', ModeType.D, "invite-only");

        registry.RegisterChannelMode(mode);

        var resolved = registry.ResolveChannelMode('i');
        Assert.NotNull(resolved);
        Assert.Equal('i', resolved.Char);
        Assert.Equal(ModeType.D, resolved.Type);
        Assert.Equal("invite-only", resolved.Name);
    }

    [Fact]
    public void ResolveChannelMode_UnknownReturnsNull()
    {
        var registry = new ModeRegistry();

        var resolved = registry.ResolveChannelMode('z');
        Assert.Null(resolved);
    }

    [Fact]
    public void RegisterStandardModes_RegistersExpectedChannelModes()
    {
        var registry = new ModeRegistry();
        registry.RegisterStandardModes();

        // Standard IRC channel modes
        Assert.NotNull(registry.ResolveChannelMode('b'));
        Assert.NotNull(registry.ResolveChannelMode('k'));
        Assert.NotNull(registry.ResolveChannelMode('l'));
        Assert.NotNull(registry.ResolveChannelMode('i'));
        Assert.NotNull(registry.ResolveChannelMode('m'));
        Assert.NotNull(registry.ResolveChannelMode('n'));
        Assert.NotNull(registry.ResolveChannelMode('t'));
        Assert.NotNull(registry.ResolveChannelMode('s'));

        // MeatSpeak custom channel modes
        Assert.NotNull(registry.ResolveChannelMode('V'));
        Assert.NotNull(registry.ResolveChannelMode('S'));
        Assert.NotNull(registry.ResolveChannelMode('E'));

        // Verify types
        Assert.Equal(ModeType.A, registry.ResolveChannelMode('b')!.Type);
        Assert.Equal(ModeType.B, registry.ResolveChannelMode('k')!.Type);
        Assert.Equal(ModeType.C, registry.ResolveChannelMode('l')!.Type);
        Assert.Equal(ModeType.D, registry.ResolveChannelMode('i')!.Type);

        // Verify custom flag
        Assert.False(registry.ResolveChannelMode('b')!.IsCustom);
        Assert.True(registry.ResolveChannelMode('V')!.IsCustom);
        Assert.True(registry.ResolveChannelMode('S')!.IsCustom);
        Assert.True(registry.ResolveChannelMode('E')!.IsCustom);
    }

    [Fact]
    public void GetChanModesIsupport_GeneratesCorrectFormat()
    {
        var registry = new ModeRegistry();
        registry.RegisterStandardModes();

        var result = registry.GetChanModesIsupport();

        // Expected: CHANMODES=be,k,l,ESVimnst
        // Type A: b,e
        // Type B: k
        // Type C: l
        // Type D: E,S,V,i,m,n,s,t (sorted)
        Assert.Equal("CHANMODES=be,k,l,ESVimnst", result);
    }

    [Fact]
    public void UserModes_RegisteredViaRegisterStandardModes()
    {
        var registry = new ModeRegistry();
        registry.RegisterStandardModes();

        Assert.NotNull(registry.ResolveUserMode('i'));
        Assert.NotNull(registry.ResolveUserMode('o'));
        Assert.NotNull(registry.ResolveUserMode('w'));

        Assert.Equal("invisible", registry.ResolveUserMode('i')!.Name);
        Assert.Equal("operator", registry.ResolveUserMode('o')!.Name);
        Assert.Equal("wallops", registry.ResolveUserMode('w')!.Name);
    }
}
