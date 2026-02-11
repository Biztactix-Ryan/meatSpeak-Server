namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

public interface IAdminMethod
{
    string Name { get; }
    Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default);
}

/// <summary>
/// Wraps a method that depends on scoped services (e.g. DbContext/repositories).
/// Creates a fresh DI scope per execution so the singleton JsonRpcProcessor can
/// hold a reference without capturing a scoped DbContext.
/// </summary>
public sealed class ScopedMethod : IAdminMethod
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly Func<IServiceProvider, IAdminMethod> _factory;
    public string Name { get; }

    public ScopedMethod(string name, IServiceScopeFactory scopeFactory, Func<IServiceProvider, IAdminMethod> factory)
    {
        Name = name;
        _scopeFactory = scopeFactory;
        _factory = factory;
    }

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var method = _factory(scope.ServiceProvider);
        return await method.ExecuteAsync(parameters, ct);
    }
}
