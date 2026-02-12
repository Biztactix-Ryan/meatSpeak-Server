namespace MeatSpeak.Server.Capabilities;

using MeatSpeak.Server.Core.Capabilities;
using MeatSpeak.Server.Core.Sessions;

public sealed class SimpleCapability : ICapability
{
    public string Name { get; }
    public string? Value { get; }

    public SimpleCapability(string name, string? value = null)
    {
        Name = name;
        Value = value;
    }

    public void OnEnabled(ISession session) { }
    public void OnDisabled(ISession session) { }
}
