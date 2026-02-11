namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;

public sealed class BanListMethod : IAdminMethod { public string Name => "ban.list"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
public sealed class BanAddMethod : IAdminMethod { public string Name => "ban.add"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
public sealed class BanRemoveMethod : IAdminMethod { public string Name => "ban.remove"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
public sealed class BanCheckMethod : IAdminMethod { public string Name => "ban.check"; public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default) => Task.FromResult<object?>(new { status = "not_implemented" }); }
