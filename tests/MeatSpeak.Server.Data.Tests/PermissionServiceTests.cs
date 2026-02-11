using Xunit;
using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Permissions;

namespace MeatSpeak.Server.Data.Tests;

public class PermissionServiceTests : IDisposable
{
    private readonly MeatSpeakDbContext _db;
    private readonly PermissionService _service;

    public PermissionServiceTests()
    {
        var options = new DbContextOptionsBuilder<MeatSpeakDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new MeatSpeakDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _service = new PermissionService(_db, "owner");
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task OwnerGetsAllPermissions()
    {
        var perms = await _service.GetServerPermissionsAsync("owner");
        Assert.True((perms & ServerPermission.Owner) != 0);
        Assert.True((perms & ServerPermission.All) == ServerPermission.All);
    }

    [Fact]
    public async Task NonOwnerGetsEveryoneDefaults()
    {
        var perms = await _service.GetServerPermissionsAsync("regularuser");
        Assert.Equal(ServerPermission.None, perms);
    }

    [Fact]
    public async Task CreateAndGetRole()
    {
        var role = await _service.CreateRoleAsync("TestRole", 5, ServerPermission.ManageBans, ChannelPermission.ViewChannel);
        Assert.NotEqual(Guid.Empty, role.Id);
        Assert.Equal("TestRole", role.Name);

        var fetched = await _service.GetRoleAsync(role.Id);
        Assert.NotNull(fetched);
        Assert.Equal("TestRole", fetched!.Name);
        Assert.Equal(ServerPermission.ManageBans, fetched.ServerPermissions);
    }

    [Fact]
    public async Task AssignRoleGrantsPermissions()
    {
        var role = await _service.CreateRoleAsync("Mod", 10, ServerPermission.ManageBans | ServerPermission.ViewUserInfo, ChannelPermission.KickMembers);
        await _service.AssignRoleAsync("testuser", role.Id);

        var perms = await _service.GetServerPermissionsAsync("testuser");
        Assert.True((perms & ServerPermission.ManageBans) != 0);
        Assert.True((perms & ServerPermission.ViewUserInfo) != 0);
    }

    [Fact]
    public async Task RevokeRoleRemovesPermissions()
    {
        var role = await _service.CreateRoleAsync("Mod", 10, ServerPermission.ManageBans, ChannelPermission.None);
        await _service.AssignRoleAsync("testuser", role.Id);
        await _service.RevokeRoleAsync("testuser", role.Id);

        var perms = await _service.GetServerPermissionsAsync("testuser");
        Assert.Equal(ServerPermission.None, perms);
    }

    [Fact]
    public async Task ChannelOverridesDenyAndAllow()
    {
        var role = await _service.CreateRoleAsync("Mod", 10, ServerPermission.None,
            ChannelPermission.ViewChannel | ChannelPermission.SendMessages | ChannelPermission.KickMembers);
        await _service.AssignRoleAsync("testuser", role.Id);

        // Deny KickMembers in #readonly
        await _service.SetChannelOverrideAsync(new ChannelOverride(role.Id, "#readonly",
            ChannelPermission.None, ChannelPermission.KickMembers));

        var perms = await _service.GetChannelPermissionsAsync("testuser", "#readonly");
        Assert.True((perms & ChannelPermission.ViewChannel) != 0);
        Assert.False((perms & ChannelPermission.KickMembers) != 0);
    }

    [Fact]
    public async Task GetAllRolesIncludesEveryone()
    {
        // Trigger @everyone creation
        await _service.GetServerPermissionsAsync("anyone");
        var roles = await _service.GetAllRolesAsync();
        Assert.Contains(roles, r => r.Id == Guid.Empty);
    }

    [Fact]
    public async Task DeleteRole()
    {
        var role = await _service.CreateRoleAsync("Temp", 1, ServerPermission.None, ChannelPermission.None);
        await _service.DeleteRoleAsync(role.Id);
        var fetched = await _service.GetRoleAsync(role.Id);
        Assert.Null(fetched);
    }
}
