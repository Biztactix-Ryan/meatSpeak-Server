namespace MeatSpeak.Server.Data.Entities;

public sealed class UserHistoryEntity
{
    public long Id { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string? Username { get; set; }
    public string? Hostname { get; set; }
    public string? Account { get; set; }
    public DateTimeOffset ConnectedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DisconnectedAt { get; set; }
    public string? QuitReason { get; set; }
}
