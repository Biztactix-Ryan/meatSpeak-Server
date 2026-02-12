namespace MeatSpeak.Server.Core.Server;

public sealed class TlsConfig
{
    public bool Enabled { get; set; }
    public int IrcTlsPort { get; set; } = 6697;
    public int WebSocketTlsPort { get; set; } = 443;

    // ACME settings
    public bool AcmeEnabled { get; set; }
    public List<string> AcmeDomains { get; set; } = new();
    public string? AcmeEmail { get; set; }
    public bool AcmeStaging { get; set; }
    public string AcmeChallengeType { get; set; } = "Http01";
    public int AcmeHttpPort { get; set; } = 80;
    public string? CloudflareApiToken { get; set; }
    public string? CloudflareZoneId { get; set; }

    // Manual cert settings
    public string? CertPath { get; set; }
    public string? CertKeyPath { get; set; }
    public string? CertPassword { get; set; }
}

public sealed class DatabaseConfig
{
    /// <summary>
    /// Database provider: "sqlite" (default), "postgresql", or "mysql".
    /// </summary>
    public string Provider { get; set; } = "sqlite";

    /// <summary>
    /// Connection string. If empty, defaults to "Data Source=meatspeak.db" for SQLite.
    /// </summary>
    public string? ConnectionString { get; set; }
}

public sealed class AdminApiConfig
{
    public List<AdminApiKeyEntry> ApiKeys { get; set; } = new();
    public List<string> IpAllowList { get; set; } = new();
}

public sealed class AdminApiKeyEntry
{
    public string Name { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty;
    public List<string>? AllowedMethods { get; set; }
}

public sealed class FloodConfig
{
    public bool Enabled { get; set; } = true;
    public int BurstLimit { get; set; } = 5;
    public double TokenIntervalSeconds { get; set; } = 2.0;
    public int ExcessFloodThreshold { get; set; } = 20;
}

public sealed class ServerConfig
{
    public string ServerName { get; set; } = "meatspeak.local";
    public string NetworkName { get; set; } = "MeatSpeak";
    public string Description { get; set; } = "MeatSpeak IRC Server";
    public string OwnerAccount { get; set; } = "admin";
    public List<string> Motd { get; set; } = new() { "Welcome to MeatSpeak!" };
    public string TcpBindAddress { get; set; } = "0.0.0.0";
    public int TcpPort { get; set; } = 6667;
    public string UdpBindAddress { get; set; } = "0.0.0.0";
    public int UdpPort { get; set; } = 6668;
    public int AdminPort { get; set; } = 6670;
    public int MaxConnections { get; set; } = 1024;
    public int MaxConnectionsPerIp { get; set; } = 10;
    public int PingInterval { get; set; } = 60;
    public int PingTimeout { get; set; } = 180;
    public int RegistrationTimeout { get; set; } = 30;
    public string Version { get; set; } = "meatspeak-0.1.0";
    public bool WebSocketEnabled { get; set; } = true;
    public int WebSocketPort { get; set; } = 6669;
    public string WebSocketPath { get; set; } = "/irc";
    public int MaxChannelsPerUser { get; set; } = 25;
    public string? ServerPassword { get; set; }
    public string? OperName { get; set; }
    public string? OperPassword { get; set; }
    public DatabaseConfig Database { get; set; } = new();
    public TlsConfig Tls { get; set; } = new();
    public AdminApiConfig AdminApi { get; set; } = new();
    public FloodConfig Flood { get; set; } = new();

    /// <summary>
    /// IPs exempt from per-IP connection limits and flood protection (e.g. benchmarks, monitoring).
    /// </summary>
    public HashSet<string> ExemptIps { get; set; } = new();
}
