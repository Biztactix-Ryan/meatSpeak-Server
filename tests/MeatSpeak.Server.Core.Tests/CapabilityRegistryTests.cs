using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Sessions;
using Xunit;

namespace MeatSpeak.Server.Core.Tests;

public class CapabilityRegistryTests
{
    private sealed class FakeCapability : ICapability
    {
        public string Name { get; }
        public string? Value { get; }

        public FakeCapability(string name, string? value = null)
        {
            Name = name;
            Value = value;
        }

        public void OnEnabled(ISession session) { }
        public void OnDisabled(ISession session) { }
    }

    [Fact]
    public void Register_And_Resolve_ReturnsCap()
    {
        var registry = new CapabilityRegistry();
        var cap = new FakeCapability("multi-prefix");

        registry.Register(cap);

        var resolved = registry.Resolve("multi-prefix");
        Assert.NotNull(resolved);
        Assert.Same(cap, resolved);
    }

    [Fact]
    public void Resolve_UnknownReturnsNull()
    {
        var registry = new CapabilityRegistry();

        var resolved = registry.Resolve("nonexistent");
        Assert.Null(resolved);
    }

    [Fact]
    public void GetCapLsString_WithValues()
    {
        var registry = new CapabilityRegistry();
        registry.Register(new FakeCapability("multi-prefix"));
        registry.Register(new FakeCapability("sasl", "PLAIN,EXTERNAL"));
        registry.Register(new FakeCapability("away-notify"));

        var result = registry.GetCapLsString();

        // Each capability should appear in the output
        Assert.Contains("multi-prefix", result);
        Assert.Contains("sasl=PLAIN,EXTERNAL", result);
        Assert.Contains("away-notify", result);

        // Entries are space-separated
        var parts = result.Split(' ');
        Assert.Equal(3, parts.Length);
    }
}
