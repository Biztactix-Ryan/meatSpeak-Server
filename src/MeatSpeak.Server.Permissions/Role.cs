namespace MeatSpeak.Server.Permissions;

public sealed class Role
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Position { get; set; }
    public ServerPermission ServerPermissions { get; set; }
    public ChannelPermission DefaultChannelPermissions { get; set; }

    public Role() { }

    public Role(Guid id, string name, int position, ServerPermission serverPerms, ChannelPermission channelPerms)
    {
        Id = id;
        Name = name;
        Position = position;
        ServerPermissions = serverPerms;
        DefaultChannelPermissions = channelPerms;
    }
}
