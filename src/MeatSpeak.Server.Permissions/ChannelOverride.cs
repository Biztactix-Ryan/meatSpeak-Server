namespace MeatSpeak.Server.Permissions;

public sealed class ChannelOverride
{
    public Guid RoleId { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public ChannelPermission Allow { get; set; }
    public ChannelPermission Deny { get; set; }

    public ChannelOverride() { }

    public ChannelOverride(Guid roleId, string channelName, ChannelPermission allow, ChannelPermission deny)
    {
        RoleId = roleId;
        ChannelName = channelName;
        Allow = allow;
        Deny = deny;
    }
}
