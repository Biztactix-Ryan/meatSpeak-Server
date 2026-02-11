namespace MeatSpeak.Server.Core.Commands;

using MeatSpeak.Server.Permissions;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequiresChannelPermissionAttribute : Attribute
{
    public ChannelPermission Permission { get; }

    public RequiresChannelPermissionAttribute(ChannelPermission permission)
    {
        Permission = permission;
    }
}
