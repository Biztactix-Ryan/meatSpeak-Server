namespace MeatSpeak.Server.AdminApi;

public sealed class ApiKeyEntry
{
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public List<string>? AllowedMethods { get; set; }
}
