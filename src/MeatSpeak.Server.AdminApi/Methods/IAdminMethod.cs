namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;

public interface IAdminMethod
{
    string Name { get; }
    Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default);
}
