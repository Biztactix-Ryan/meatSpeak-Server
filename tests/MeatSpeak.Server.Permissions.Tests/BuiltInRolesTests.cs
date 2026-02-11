using MeatSpeak.Server.Permissions;
using Xunit;

namespace MeatSpeak.Server.Permissions.Tests;

public class BuiltInRolesTests
{
    [Fact]
    public void Everyone_HasGuidEmpty_Position0_CorrectDefaultPerms()
    {
        var everyone = BuiltInRoles.Everyone();

        Assert.Equal(Guid.Empty, everyone.Id);
        Assert.Equal("@everyone", everyone.Name);
        Assert.Equal(0, everyone.Position);
        Assert.Equal(ServerPermission.None, everyone.ServerPermissions);

        var expectedChannel =
            ChannelPermission.ViewChannel |
            ChannelPermission.SendMessages |
            ChannelPermission.EmbedLinks |
            ChannelPermission.VoiceConnect |
            ChannelPermission.VoiceSpeak |
            ChannelPermission.VoiceUseVAD;

        Assert.Equal(expectedChannel, everyone.DefaultChannelPermissions);
    }

    [Fact]
    public void Moderator_HasPosition10_ManageBansPerm()
    {
        var mod = BuiltInRoles.Moderator();

        Assert.Equal("Moderator", mod.Name);
        Assert.Equal(10, mod.Position);
        Assert.True((mod.ServerPermissions & ServerPermission.ManageBans) != 0);
        Assert.True((mod.ServerPermissions & ServerPermission.ViewUserInfo) != 0);
        Assert.True((mod.ServerPermissions & ServerPermission.ManageNicknames) != 0);
    }

    [Fact]
    public void Admin_HasAllServerPermsMinusOwner()
    {
        var admin = BuiltInRoles.Admin();

        Assert.Equal("Admin", admin.Name);
        Assert.Equal(20, admin.Position);

        var expectedServerPerms = ServerPermission.All & ~ServerPermission.Owner;
        Assert.Equal(expectedServerPerms, admin.ServerPermissions);

        // Owner flag must not be set
        Assert.True((admin.ServerPermissions & ServerPermission.Owner) == 0);

        // All channel permissions
        Assert.Equal(ChannelPermission.All, admin.DefaultChannelPermissions);
    }
}
