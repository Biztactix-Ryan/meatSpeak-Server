using MeatSpeak.Server.Permissions;
using Xunit;

namespace MeatSpeak.Server.Permissions.Tests;

public class PermissionResolutionTests
{
    private static readonly string OwnerAccount = "owner@example.com";
    private static readonly string RegularAccount = "user@example.com";

    private static Role MakeEveryoneRole(
        ServerPermission serverPerms = ServerPermission.None,
        ChannelPermission channelPerms = ChannelPermission.ViewChannel | ChannelPermission.SendMessages)
    {
        return new Role(Guid.Empty, "@everyone", 0, serverPerms, channelPerms);
    }

    private static Role MakeRole(
        string name,
        int position,
        ServerPermission serverPerms,
        ChannelPermission channelPerms = ChannelPermission.None)
    {
        return new Role(Guid.NewGuid(), name, position, serverPerms, channelPerms);
    }

    // --- ComputeServerPermissions ---

    [Fact]
    public void ComputeServerPermissions_OwnerGetsAllPlusOwnerFlag()
    {
        var everyone = MakeEveryoneRole();
        var result = PermissionResolution.ComputeServerPermissions(
            OwnerAccount, OwnerAccount, everyone, Array.Empty<Role>());

        Assert.True((result & ServerPermission.Owner) != 0);
        Assert.True((result & ServerPermission.All) == ServerPermission.All);
    }

    [Fact]
    public void ComputeServerPermissions_NonOwnerGetsEveryonePlusAssignedRoles()
    {
        var everyone = MakeEveryoneRole(serverPerms: ServerPermission.ViewUserInfo);
        var role = MakeRole("Mod", 10, ServerPermission.ManageBans);

        var result = PermissionResolution.ComputeServerPermissions(
            OwnerAccount, RegularAccount, everyone, new[] { role });

        Assert.True((result & ServerPermission.ViewUserInfo) != 0);
        Assert.True((result & ServerPermission.ManageBans) != 0);
        Assert.True((result & ServerPermission.Owner) == 0);
    }

    [Fact]
    public void ComputeServerPermissions_MultipleRolesOrTogether()
    {
        var everyone = MakeEveryoneRole();
        var role1 = MakeRole("Mod", 10, ServerPermission.ManageBans);
        var role2 = MakeRole("Helper", 5, ServerPermission.ViewUserInfo);

        var result = PermissionResolution.ComputeServerPermissions(
            OwnerAccount, RegularAccount, everyone, new[] { role1, role2 });

        Assert.True((result & ServerPermission.ManageBans) != 0);
        Assert.True((result & ServerPermission.ViewUserInfo) != 0);
    }

    // --- HasServerPermission ---

    [Fact]
    public void HasServerPermission_OwnerFlagBypassesAllChecks()
    {
        // Owner flag set but no ManageServer permission explicitly
        var perms = ServerPermission.Owner;
        Assert.True(PermissionResolution.HasServerPermission(perms, ServerPermission.ManageServer));
        Assert.True(PermissionResolution.HasServerPermission(perms, ServerPermission.ManageBans));
        Assert.True(PermissionResolution.HasServerPermission(perms, ServerPermission.All));
    }

    [Fact]
    public void HasServerPermission_ReturnsTrueForExactMatch()
    {
        var perms = ServerPermission.ManageBans | ServerPermission.ViewUserInfo;
        Assert.True(PermissionResolution.HasServerPermission(perms, ServerPermission.ManageBans));
    }

    [Fact]
    public void HasServerPermission_ReturnsFalseWhenMissing()
    {
        var perms = ServerPermission.ManageBans;
        Assert.False(PermissionResolution.HasServerPermission(perms, ServerPermission.ManageServer));
    }

    // --- ComputeChannelPermissions ---

    [Fact]
    public void ComputeChannelPermissions_OwnerGetsAll()
    {
        var everyone = MakeEveryoneRole();
        var result = PermissionResolution.ComputeChannelPermissions(
            OwnerAccount, OwnerAccount, everyone, Array.Empty<Role>(), Array.Empty<ChannelOverride>());

        Assert.Equal(ChannelPermission.All, result);
    }

    [Fact]
    public void ComputeChannelPermissions_EveryoneDefaults()
    {
        var everyone = MakeEveryoneRole(
            channelPerms: ChannelPermission.ViewChannel | ChannelPermission.SendMessages);

        var result = PermissionResolution.ComputeChannelPermissions(
            OwnerAccount, RegularAccount, everyone, Array.Empty<Role>(), Array.Empty<ChannelOverride>());

        Assert.Equal(ChannelPermission.ViewChannel | ChannelPermission.SendMessages, result);
    }

    [Fact]
    public void ComputeChannelPermissions_RoleOverridesApply_DenyRemovesAllowAdds()
    {
        var everyone = MakeEveryoneRole(
            channelPerms: ChannelPermission.ViewChannel | ChannelPermission.SendMessages);

        var roleId = Guid.NewGuid();
        var role = new Role(roleId, "Mod", 10, ServerPermission.None, ChannelPermission.None);

        // Override: deny SendMessages, allow SetTopic
        var channelOverride = new ChannelOverride(roleId, "#test",
            ChannelPermission.SetTopic, ChannelPermission.SendMessages);

        var result = PermissionResolution.ComputeChannelPermissions(
            OwnerAccount, RegularAccount, everyone, new[] { role }, new[] { channelOverride });

        // SendMessages should be denied
        Assert.True((result & ChannelPermission.SendMessages) == 0);
        // SetTopic should be allowed
        Assert.True((result & ChannelPermission.SetTopic) != 0);
        // ViewChannel should remain
        Assert.True((result & ChannelPermission.ViewChannel) != 0);
    }

    [Fact]
    public void ComputeChannelPermissions_EveryoneChannelOverrideAppliesBeforeRoleOverrides()
    {
        var everyoneId = Guid.Empty;
        var everyone = MakeEveryoneRole(
            channelPerms: ChannelPermission.ViewChannel | ChannelPermission.SendMessages | ChannelPermission.EmbedLinks);

        // @everyone override denies SendMessages
        var everyoneOverride = new ChannelOverride(everyoneId, "#test",
            ChannelPermission.None, ChannelPermission.SendMessages);

        var roleId = Guid.NewGuid();
        var role = new Role(roleId, "Mod", 10, ServerPermission.None, ChannelPermission.None);

        // Role override allows SendMessages back
        var roleOverride = new ChannelOverride(roleId, "#test",
            ChannelPermission.SendMessages, ChannelPermission.None);

        var result = PermissionResolution.ComputeChannelPermissions(
            OwnerAccount, RegularAccount, everyone,
            new[] { role },
            new[] { everyoneOverride, roleOverride });

        // @everyone override denied SendMessages, but role override re-allows it
        Assert.True((result & ChannelPermission.SendMessages) != 0);
        // ViewChannel and EmbedLinks remain from @everyone defaults
        Assert.True((result & ChannelPermission.ViewChannel) != 0);
        Assert.True((result & ChannelPermission.EmbedLinks) != 0);
    }

    // --- CanManageRole ---

    [Fact]
    public void CanManageRole_HigherPositionCanManageLower()
    {
        var actorRole = new Role(Guid.NewGuid(), "Admin", 20, ServerPermission.None, ChannelPermission.None);
        var targetRole = new Role(Guid.NewGuid(), "Mod", 10, ServerPermission.None, ChannelPermission.None);

        Assert.True(PermissionResolution.CanManageRole(new[] { actorRole }, targetRole));
    }

    [Fact]
    public void CanManageRole_SamePositionCannotManage()
    {
        var actorRole = new Role(Guid.NewGuid(), "Mod1", 10, ServerPermission.None, ChannelPermission.None);
        var targetRole = new Role(Guid.NewGuid(), "Mod2", 10, ServerPermission.None, ChannelPermission.None);

        Assert.False(PermissionResolution.CanManageRole(new[] { actorRole }, targetRole));
    }

    [Fact]
    public void CanManageRole_LowerPositionCannotManageHigher()
    {
        var actorRole = new Role(Guid.NewGuid(), "Mod", 10, ServerPermission.None, ChannelPermission.None);
        var targetRole = new Role(Guid.NewGuid(), "Admin", 20, ServerPermission.None, ChannelPermission.None);

        Assert.False(PermissionResolution.CanManageRole(new[] { actorRole }, targetRole));
    }
}
