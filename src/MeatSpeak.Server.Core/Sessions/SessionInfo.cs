namespace MeatSpeak.Server.Core.Sessions;

using System.Diagnostics;
using MeatSpeak.Server.Core.Capabilities;

public sealed class SessionInfo
{
    public long ConnectTimestamp { get; set; } = Stopwatch.GetTimestamp();
    public string? Nickname { get; set; }
    public string? Username { get; set; }
    public string? Realname { get; set; }
    public string? Hostname { get; set; }
    public string? Account { get; set; }
    public string? ServerPassword { get; set; }
    public HashSet<char> UserModes { get; } = new();
    public HashSet<string> Channels { get; } = new(StringComparer.OrdinalIgnoreCase);
    public CapNegotiationState CapState { get; } = new();
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastActivity { get; set; } = DateTimeOffset.UtcNow;
    public string? AwayMessage { get; set; }

    public string Prefix => Nickname != null && Username != null && Hostname != null
        ? $"{Nickname}!{Username}@{Hostname}"
        : Nickname ?? "*";
}
