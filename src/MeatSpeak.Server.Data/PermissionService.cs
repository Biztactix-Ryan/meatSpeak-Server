namespace MeatSpeak.Server.Data;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Permissions;
using MeatSpeak.Server.Data.Entities;

public sealed class PermissionService : IPermissionService
{
    private readonly MeatSpeakDbContext _db;
    private readonly string _ownerAccount;

    public PermissionService(MeatSpeakDbContext db, string ownerAccount)
    {
        _db = db;
        _ownerAccount = ownerAccount;
    }

    public async Task<ServerPermission> GetServerPermissionsAsync(string account, CancellationToken ct = default)
    {
        var everyoneRole = await GetOrCreateEveryoneRoleAsync(ct);
        var assignedRoles = await GetRoleEntitiesForAccountAsync(account, ct);
        return PermissionResolution.ComputeServerPermissions(
            _ownerAccount, account, ToRole(everyoneRole), assignedRoles.Select(ToRole).ToList());
    }

    public async Task<ChannelPermission> GetChannelPermissionsAsync(string account, string channelName, CancellationToken ct = default)
    {
        var everyoneRole = await GetOrCreateEveryoneRoleAsync(ct);
        var assignedRoleEntities = await GetRoleEntitiesForAccountAsync(account, ct);
        var overrideEntities = await _db.ChannelOverrides.Where(o => o.ChannelName == channelName).ToListAsync(ct);

        return PermissionResolution.ComputeChannelPermissions(
            _ownerAccount, account,
            ToRole(everyoneRole),
            assignedRoleEntities.Select(ToRole).ToList(),
            overrideEntities.Select(ToOverride).ToList());
    }

    public async Task<IReadOnlyList<Role>> GetRolesForAccountAsync(string account, CancellationToken ct = default)
    {
        var entities = await GetRoleEntitiesForAccountAsync(account, ct);
        return entities.Select(ToRole).ToList();
    }

    public async Task<Role?> GetRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        var entity = await _db.Roles.FindAsync(new object[] { roleId }, ct);
        return entity != null ? ToRole(entity) : null;
    }

    public async Task<IReadOnlyList<Role>> GetAllRolesAsync(CancellationToken ct = default)
    {
        var entities = await _db.Roles.OrderBy(r => r.Position).ToListAsync(ct);
        return entities.Select(ToRole).ToList();
    }

    public async Task<Role> CreateRoleAsync(string name, int position, ServerPermission serverPerms, ChannelPermission channelPerms, CancellationToken ct = default)
    {
        var entity = new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = name,
            Position = position,
            ServerPermissions = (ulong)serverPerms,
            DefaultChannelPermissions = (ulong)channelPerms,
        };
        _db.Roles.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToRole(entity);
    }

    public async Task UpdateRoleAsync(Role role, CancellationToken ct = default)
    {
        var entity = await _db.Roles.FindAsync(new object[] { role.Id }, ct);
        if (entity == null) return;
        entity.Name = role.Name;
        entity.Position = role.Position;
        entity.ServerPermissions = (ulong)role.ServerPermissions;
        entity.DefaultChannelPermissions = (ulong)role.DefaultChannelPermissions;
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        var entity = await _db.Roles.FindAsync(new object[] { roleId }, ct);
        if (entity != null)
        {
            _db.Roles.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task AssignRoleAsync(string account, Guid roleId, CancellationToken ct = default)
    {
        var exists = await _db.UserRoles.AnyAsync(ur => ur.Account == account && ur.RoleId == roleId, ct);
        if (!exists)
        {
            _db.UserRoles.Add(new UserRoleEntity { Account = account, RoleId = roleId });
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeRoleAsync(string account, Guid roleId, CancellationToken ct = default)
    {
        var ur = await _db.UserRoles.FirstOrDefaultAsync(x => x.Account == account && x.RoleId == roleId, ct);
        if (ur != null)
        {
            _db.UserRoles.Remove(ur);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<ChannelOverride>> GetChannelOverridesAsync(string channelName, CancellationToken ct = default)
    {
        var entities = await _db.ChannelOverrides.Where(o => o.ChannelName == channelName).ToListAsync(ct);
        return entities.Select(ToOverride).ToList();
    }

    public async Task SetChannelOverrideAsync(ChannelOverride channelOverride, CancellationToken ct = default)
    {
        var entity = await _db.ChannelOverrides.FirstOrDefaultAsync(
            o => o.RoleId == channelOverride.RoleId && o.ChannelName == channelOverride.ChannelName, ct);
        if (entity != null)
        {
            entity.Allow = (ulong)channelOverride.Allow;
            entity.Deny = (ulong)channelOverride.Deny;
        }
        else
        {
            _db.ChannelOverrides.Add(new ChannelOverrideEntity
            {
                RoleId = channelOverride.RoleId,
                ChannelName = channelOverride.ChannelName,
                Allow = (ulong)channelOverride.Allow,
                Deny = (ulong)channelOverride.Deny,
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteChannelOverrideAsync(Guid roleId, string channelName, CancellationToken ct = default)
    {
        var entity = await _db.ChannelOverrides.FirstOrDefaultAsync(
            o => o.RoleId == roleId && o.ChannelName == channelName, ct);
        if (entity != null)
        {
            _db.ChannelOverrides.Remove(entity);
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<RoleEntity> GetOrCreateEveryoneRoleAsync(CancellationToken ct)
    {
        var everyone = await _db.Roles.FindAsync(new object[] { Guid.Empty }, ct);
        if (everyone != null) return everyone;

        var builtin = BuiltInRoles.Everyone();
        everyone = new RoleEntity
        {
            Id = Guid.Empty,
            Name = builtin.Name,
            Position = builtin.Position,
            ServerPermissions = (ulong)builtin.ServerPermissions,
            DefaultChannelPermissions = (ulong)builtin.DefaultChannelPermissions,
        };
        _db.Roles.Add(everyone);
        await _db.SaveChangesAsync(ct);
        return everyone;
    }

    private async Task<List<RoleEntity>> GetRoleEntitiesForAccountAsync(string account, CancellationToken ct)
        => await _db.UserRoles
            .Where(ur => ur.Account == account)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role!)
            .ToListAsync(ct);

    private static Role ToRole(RoleEntity e) => new(e.Id, e.Name, e.Position,
        (ServerPermission)e.ServerPermissions, (ChannelPermission)e.DefaultChannelPermissions);

    private static ChannelOverride ToOverride(ChannelOverrideEntity e) => new(e.RoleId, e.ChannelName,
        (ChannelPermission)e.Allow, (ChannelPermission)e.Deny);
}
