namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;

public sealed class RoleListMethod : IAdminMethod { public string Name => "role.list"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
public sealed class RoleGetMethod : IAdminMethod { public string Name => "role.get"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
public sealed class RoleCreateMethod : IAdminMethod { public string Name => "role.create"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
public sealed class RoleUpdateMethod : IAdminMethod { public string Name => "role.update"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
public sealed class RoleDeleteMethod : IAdminMethod { public string Name => "role.delete"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
public sealed class RoleAssignMethod : IAdminMethod { public string Name => "role.assign"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
public sealed class RoleRevokeMethod : IAdminMethod { public string Name => "role.revoke"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
public sealed class RoleMembersMethod : IAdminMethod { public string Name => "role.members"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
