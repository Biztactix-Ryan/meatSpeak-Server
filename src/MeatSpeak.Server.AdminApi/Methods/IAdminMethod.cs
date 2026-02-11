namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;

public interface IAdminMethod
{
    string Name { get; }
    Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default);
}

/// <summary>
/// Placeholder for methods whose backing service (e.g. database) is not configured.
/// </summary>
public sealed class StubMethod : IAdminMethod
{
    public string Name { get; }
    public StubMethod(string name) => Name = name;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { error = "not_available", message = "Database is not configured" });
}
