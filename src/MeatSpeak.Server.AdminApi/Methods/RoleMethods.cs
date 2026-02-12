namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Data.Repositories;
using MeatSpeak.Server.Permissions;

public sealed class RoleListMethod : IAdminMethod
{
    private readonly IPermissionService _perms;
    public string Name => "role.list";
    public RoleListMethod(IPermissionService perms) => _perms = perms;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var roles = await _perms.GetAllRolesAsync(ct);
        return new
        {
            roles = roles.Select(r => new
            {
                id = r.Id,
                name = r.Name,
                position = r.Position,
                server_permissions = (ulong)r.ServerPermissions,
                channel_permissions = (ulong)r.DefaultChannelPermissions,
            }).ToList()
        };
    }
}

public sealed class RoleGetMethod : IAdminMethod
{
    private readonly IPermissionService _perms;
    public string Name => "role.get";
    public RoleGetMethod(IPermissionService perms) => _perms = perms;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var id = AdminParamHelper.RequireGuid(p, "id");

        var role = await _perms.GetRoleAsync(id, ct);
        if (role == null)
            return new { error = "role_not_found" };

        return new
        {
            id = role.Id,
            name = role.Name,
            position = role.Position,
            server_permissions = (ulong)role.ServerPermissions,
            channel_permissions = (ulong)role.DefaultChannelPermissions,
        };
    }
}

public sealed class RoleCreateMethod : IAdminMethod
{
    private readonly IPermissionService _perms;
    public string Name => "role.create";
    public RoleCreateMethod(IPermissionService perms) => _perms = perms;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var name = AdminParamHelper.RequireString(p, "name");
        var position = p.GetProperty("position").GetInt32();
        var serverPerms = p.GetProperty("server_permissions").GetUInt64();
        var channelPerms = p.GetProperty("channel_permissions").GetUInt64();

        var role = await _perms.CreateRoleAsync(name, position,
            (ServerPermission)serverPerms, (ChannelPermission)channelPerms, ct);

        return new { status = "ok", id = role.Id };
    }
}

public sealed class RoleUpdateMethod : IAdminMethod
{
    private readonly IPermissionService _perms;
    public string Name => "role.update";
    public RoleUpdateMethod(IPermissionService perms) => _perms = perms;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var id = AdminParamHelper.RequireGuid(p, "id");

        var role = await _perms.GetRoleAsync(id, ct);
        if (role == null)
            return new { error = "role_not_found" };

        if (p.TryGetProperty("name", out var nameEl))
            role.Name = nameEl.GetString() ?? role.Name;
        if (p.TryGetProperty("position", out var posEl))
            role.Position = posEl.GetInt32();
        if (p.TryGetProperty("server_permissions", out var spEl))
            role.ServerPermissions = (ServerPermission)spEl.GetUInt64();
        if (p.TryGetProperty("channel_permissions", out var cpEl))
            role.DefaultChannelPermissions = (ChannelPermission)cpEl.GetUInt64();

        await _perms.UpdateRoleAsync(role, ct);
        return new { status = "ok" };
    }
}

public sealed class RoleDeleteMethod : IAdminMethod
{
    private readonly IPermissionService _perms;
    public string Name => "role.delete";
    public RoleDeleteMethod(IPermissionService perms) => _perms = perms;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var id = AdminParamHelper.RequireGuid(p, "id");

        await _perms.DeleteRoleAsync(id, ct);
        return new { status = "ok" };
    }
}

public sealed class RoleAssignMethod : IAdminMethod
{
    private readonly IPermissionService _perms;
    public string Name => "role.assign";
    public RoleAssignMethod(IPermissionService perms) => _perms = perms;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var account = AdminParamHelper.RequireString(p, "account");
        var roleId = AdminParamHelper.RequireGuid(p, "role_id");

        await _perms.AssignRoleAsync(account, roleId, ct);
        return new { status = "ok" };
    }
}

public sealed class RoleRevokeMethod : IAdminMethod
{
    private readonly IPermissionService _perms;
    public string Name => "role.revoke";
    public RoleRevokeMethod(IPermissionService perms) => _perms = perms;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var account = AdminParamHelper.RequireString(p, "account");
        var roleId = AdminParamHelper.RequireGuid(p, "role_id");

        await _perms.RevokeRoleAsync(account, roleId, ct);
        return new { status = "ok" };
    }
}

public sealed class RoleMembersMethod : IAdminMethod
{
    private readonly IRoleRepository _roles;
    public string Name => "role.members";
    public RoleMembersMethod(IRoleRepository roles) => _roles = roles;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var roleId = AdminParamHelper.RequireGuid(p, "role_id");

        var accounts = await _roles.GetAccountsWithRoleAsync(roleId, ct);
        return new { accounts };
    }
}
