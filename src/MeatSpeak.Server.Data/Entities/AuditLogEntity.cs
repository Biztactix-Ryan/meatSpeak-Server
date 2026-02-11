namespace MeatSpeak.Server.Data.Entities;

public sealed class AuditLogEntity
{
    public long Id { get; set; }
    public string Action { get; set; } = string.Empty;
    public string Actor { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
}
