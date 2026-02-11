namespace MeatSpeak.Server.Data.Entities;

public sealed class RoleEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }
    public ulong ServerPermissions { get; set; }
    public ulong DefaultChannelPermissions { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();
    public ICollection<ChannelOverrideEntity> ChannelOverrides { get; set; } = new List<ChannelOverrideEntity>();
}
