namespace MeatSpeak.Server.Data.Entities;

public sealed class UserAccountEntity
{
    public Guid Id { get; set; }
    public string Account { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastLogin { get; set; }
    public bool Disabled { get; set; }
}
