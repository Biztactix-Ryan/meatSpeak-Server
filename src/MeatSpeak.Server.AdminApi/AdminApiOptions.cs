namespace MeatSpeak.Server.AdminApi;

public sealed class AdminApiOptions
{
    public string BindAddress { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 6670;
    public bool UseTls { get; set; }
    public string? TlsCertPath { get; set; }
    public List<ApiKeyEntry> ApiKeys { get; set; } = new();
}

public sealed class ApiKeyEntry
{
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public List<string>? AllowedMethods { get; set; }
}
