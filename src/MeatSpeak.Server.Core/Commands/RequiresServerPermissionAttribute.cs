namespace MeatSpeak.Server.Core.Commands;

using MeatSpeak.Server.Permissions;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class RequiresServerPermissionAttribute : Attribute
{
    public ServerPermission Permission { get; }

    public RequiresServerPermissionAttribute(ServerPermission permission)
    {
        Permission = permission;
    }
}
