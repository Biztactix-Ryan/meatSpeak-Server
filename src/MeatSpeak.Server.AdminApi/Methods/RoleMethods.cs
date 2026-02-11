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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var idStr = parameters.Value.GetProperty("id").GetString()
            ?? throw new JsonException("Missing 'id'");

        if (!Guid.TryParse(idStr, out var id))
            throw new JsonException("Invalid 'id' format");

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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var name = parameters.Value.GetProperty("name").GetString()
            ?? throw new JsonException("Missing 'name'");
        var position = parameters.Value.GetProperty("position").GetInt32();
        var serverPerms = parameters.Value.GetProperty("server_permissions").GetUInt64();
        var channelPerms = parameters.Value.GetProperty("channel_permissions").GetUInt64();

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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var idStr = parameters.Value.GetProperty("id").GetString()
            ?? throw new JsonException("Missing 'id'");

        if (!Guid.TryParse(idStr, out var id))
            throw new JsonException("Invalid 'id' format");

        var role = await _perms.GetRoleAsync(id, ct);
        if (role == null)
            return new { error = "role_not_found" };

        if (parameters.Value.TryGetProperty("name", out var nameEl))
            role.Name = nameEl.GetString() ?? role.Name;
        if (parameters.Value.TryGetProperty("position", out var posEl))
            role.Position = posEl.GetInt32();
        if (parameters.Value.TryGetProperty("server_permissions", out var spEl))
            role.ServerPermissions = (ServerPermission)spEl.GetUInt64();
        if (parameters.Value.TryGetProperty("channel_permissions", out var cpEl))
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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var idStr = parameters.Value.GetProperty("id").GetString()
            ?? throw new JsonException("Missing 'id'");

        if (!Guid.TryParse(idStr, out var id))
            throw new JsonException("Invalid 'id' format");

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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var account = parameters.Value.GetProperty("account").GetString()
            ?? throw new JsonException("Missing 'account'");
        var roleIdStr = parameters.Value.GetProperty("role_id").GetString()
            ?? throw new JsonException("Missing 'role_id'");

        if (!Guid.TryParse(roleIdStr, out var roleId))
            throw new JsonException("Invalid 'role_id' format");

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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var account = parameters.Value.GetProperty("account").GetString()
            ?? throw new JsonException("Missing 'account'");
        var roleIdStr = parameters.Value.GetProperty("role_id").GetString()
            ?? throw new JsonException("Missing 'role_id'");

        if (!Guid.TryParse(roleIdStr, out var roleId))
            throw new JsonException("Invalid 'role_id' format");

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
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var roleIdStr = parameters.Value.GetProperty("role_id").GetString()
            ?? throw new JsonException("Missing 'role_id'");

        if (!Guid.TryParse(roleIdStr, out var roleId))
            throw new JsonException("Invalid 'role_id' format");

        var accounts = await _roles.GetAccountsWithRoleAsync(roleId, ct);
        return new { accounts };
    }
}
