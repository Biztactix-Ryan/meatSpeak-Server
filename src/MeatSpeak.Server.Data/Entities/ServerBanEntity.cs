namespace MeatSpeak.Server.Data.Entities;

public sealed class ServerBanEntity
{
    public Guid Id { get; set; }
    public string Mask { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string SetBy { get; set; } = string.Empty;
    public DateTimeOffset SetAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAt { get; set; }
}
