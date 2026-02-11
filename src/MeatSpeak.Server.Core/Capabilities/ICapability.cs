namespace MeatSpeak.Server.Core.Capabilities;

using MeatSpeak.Server.Core.Sessions;

public interface ICapability
{
    string Name { get; }
    string? Value { get; }
    void OnEnabled(ISession session);
    void OnDisabled(ISession session);
}
