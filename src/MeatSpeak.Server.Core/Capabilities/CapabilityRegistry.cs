namespace MeatSpeak.Server.Core.Capabilities;

public sealed class CapabilityRegistry
{
    private readonly Dictionary<string, ICapability> _capabilities = new(StringComparer.OrdinalIgnoreCase);

    public void Register(ICapability capability)
    {
        _capabilities[capability.Name] = capability;
    }

    public ICapability? Resolve(string name) =>
        _capabilities.TryGetValue(name, out var cap) ? cap : null;

    public IReadOnlyDictionary<string, ICapability> All => _capabilities;

    /// <summary>
    /// Builds the CAP LS response string: "cap1 cap2=value cap3"
    /// </summary>
    public string GetCapLsString()
    {
        var parts = new List<string>();
        foreach (var cap in _capabilities.Values)
        {
            parts.Add(cap.Value != null ? $"{cap.Name}={cap.Value}" : cap.Name);
        }
        return string.Join(" ", parts);
    }
}
