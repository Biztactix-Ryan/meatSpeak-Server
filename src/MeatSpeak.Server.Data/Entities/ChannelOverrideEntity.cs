namespace MeatSpeak.Server.Data.Entities;

public sealed class ChannelOverrideEntity
{
    public Guid RoleId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public ulong Allow { get; set; }
    public ulong Deny { get; set; }

    public RoleEntity? Role { get; set; }
}
