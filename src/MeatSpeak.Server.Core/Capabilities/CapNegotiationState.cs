namespace MeatSpeak.Server.Core.Capabilities;

public sealed class CapNegotiationState
{
    public HashSet<string> Requested { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Acknowledged { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool NegotiationComplete { get; set; }
    public bool InNegotiation { get; set; }
}
