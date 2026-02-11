namespace MeatSpeak.Server.Permissions;

public static class PermissionResolution
{
    public static ServerPermission ComputeServerPermissions(
        string ownerAccount,
        string userAccount,
        Role everyoneRole,
        IReadOnlyList<Role> assignedRoles)
    {
        if (string.Equals(ownerAccount, userAccount, StringComparison.OrdinalIgnoreCase))
            return ServerPermission.All | ServerPermission.Owner;

        var perms = everyoneRole.ServerPermissions;
        foreach (var role in assignedRoles)
            perms |= role.ServerPermissions;

        return perms;
    }

    public static ChannelPermission ComputeChannelPermissions(
        string ownerAccount,
        string userAccount,
        Role everyoneRole,
        IReadOnlyList<Role> assignedRoles,
        IReadOnlyList<ChannelOverride> overrides)
    {
        if (string.Equals(ownerAccount, userAccount, StringComparison.OrdinalIgnoreCase))
            return ChannelPermission.All;

        // Start with @everyone defaults
        var perms = everyoneRole.DefaultChannelPermissions;

        // OR in all assigned role defaults
        foreach (var role in assignedRoles)
            perms |= role.DefaultChannelPermissions;

        // Apply @everyone channel override first
        var everyoneOverride = FindOverride(overrides, everyoneRole.Id);
        if (everyoneOverride != null)
        {
            perms &= ~everyoneOverride.Deny;
            perms |= everyoneOverride.Allow;
        }

        // Collect all role overrides (OR allows, OR denies)
        var roleAllow = ChannelPermission.None;
        var roleDeny = ChannelPermission.None;
        foreach (var role in assignedRoles)
        {
            var over = FindOverride(overrides, role.Id);
            if (over != null)
            {
                roleAllow |= over.Allow;
                roleDeny |= over.Deny;
            }
        }

        perms &= ~roleDeny;
        perms |= roleAllow;

        return perms;
    }

    public static bool HasServerPermission(ServerPermission permissions, ServerPermission required)
    {
        if ((permissions & ServerPermission.Owner) != 0)
            return true;
        return (permissions & required) == required;
    }

    public static bool HasChannelPermission(ChannelPermission permissions, ChannelPermission required)
    {
        return (permissions & required) == required;
    }

    public static bool CanManageRole(IReadOnlyList<Role> actorRoles, Role targetRole)
    {
        if (actorRoles.Count == 0) return false;
        int highestPosition = 0;
        foreach (var r in actorRoles)
            if (r.Position > highestPosition)
                highestPosition = r.Position;
        return highestPosition > targetRole.Position;
    }

    private static ChannelOverride? FindOverride(IReadOnlyList<ChannelOverride> overrides, Guid roleId)
    {
        foreach (var o in overrides)
            if (o.RoleId == roleId)
                return o;
        return null;
    }
}
