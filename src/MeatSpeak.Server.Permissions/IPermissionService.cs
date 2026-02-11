namespace MeatSpeak.Server.Permissions;

public interface IPermissionService
{
    Task<ServerPermission> GetServerPermissionsAsync(string account, CancellationToken ct = default);
    Task<ChannelPermission> GetChannelPermissionsAsync(string account, string channelName, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetRolesForAccountAsync(string account, CancellationToken ct = default);
    Task<Role?> GetRoleAsync(Guid roleId, CancellationToken ct = default);
    Task<IReadOnlyList<Role>> GetAllRolesAsync(CancellationToken ct = default);
    Task<Role> CreateRoleAsync(string name, int position, ServerPermission serverPerms, ChannelPermission channelPerms, CancellationToken ct = default);
    Task UpdateRoleAsync(Role role, CancellationToken ct = default);
    Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default);
    Task AssignRoleAsync(string account, Guid roleId, CancellationToken ct = default);
    Task RevokeRoleAsync(string account, Guid roleId, CancellationToken ct = default);
    Task<IReadOnlyList<ChannelOverride>> GetChannelOverridesAsync(string channelName, CancellationToken ct = default);
    Task SetChannelOverrideAsync(ChannelOverride channelOverride, CancellationToken ct = default);
    Task DeleteChannelOverrideAsync(Guid roleId, string channelName, CancellationToken ct = default);
}
