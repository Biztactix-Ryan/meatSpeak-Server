namespace MeatSpeak.Server.Data.Entities;

public sealed class UserRoleEntity
{
    public string Account { get; set; } = string.Empty;
    public Guid RoleId { get; set; }
    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;

    public RoleEntity? Role { get; set; }
}
