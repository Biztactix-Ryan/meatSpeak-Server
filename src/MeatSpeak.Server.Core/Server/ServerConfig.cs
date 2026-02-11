namespace MeatSpeak.Server.Core.Server;

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
    public int PingInterval { get; set; } = 60;
    public int PingTimeout { get; set; } = 180;
    public string Version { get; set; } = "meatspeak-0.1.0";
    public string? OperName { get; set; }
    public string? OperPassword { get; set; }
}
