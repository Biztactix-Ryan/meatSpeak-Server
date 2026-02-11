using System.Text.Json;
using Xunit;
using MeatSpeak.Server.AdminApi.Methods;
using Microsoft.Extensions.DependencyInjection;

namespace MeatSpeak.Server.AdminApi.Tests;

public class ScopedMethodTests
{
    [Fact]
    public void ScopedMethod_HasCorrectName()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

        var method = new ScopedMethod("test.method", scopeFactory,
            _ => new FakeMethod("test.method", new { ok = true }));

        Assert.Equal("test.method", method.Name);
    }

    [Fact]
    public async Task ScopedMethod_DelegatesToInnerMethod()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

        var expected = new { value = 42 };
        var method = new ScopedMethod("test.method", scopeFactory,
            _ => new FakeMethod("test.method", expected));

        var result = await method.ExecuteAsync(null);
        Assert.Same(expected, result);
    }

    [Fact]
    public async Task ScopedMethod_PassesParametersThrough()
    {
        var services = new ServiceCollection().BuildServiceProvider();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();

        JsonElement? capturedParams = null;
        var method = new ScopedMethod("test.method", scopeFactory,
            _ => new CapturingMethod("test.method", p => { capturedParams = p; }));

        var paramsJson = JsonDocument.Parse("""{"key":"value"}""").RootElement;
        await method.ExecuteAsync(paramsJson);

        Assert.NotNull(capturedParams);
        Assert.Equal("value", capturedParams!.Value.GetProperty("key").GetString());
    }

    [Fact]
    public async Task ScopedMethod_ResolvesFromScope()
    {
        // Register a scoped service and verify the factory receives the scoped provider
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        ScopedDependency? resolved = null;
        var method = new ScopedMethod("test.method", scopeFactory, svc =>
        {
            resolved = svc.GetRequiredService<ScopedDependency>();
            return new FakeMethod("test.method", new { ok = true });
        });

        await method.ExecuteAsync(null);
        Assert.NotNull(resolved);
    }

    [Fact]
    public async Task ScopedMethod_CreatesFreshScopePerCall()
    {
        var services = new ServiceCollection();
        services.AddScoped<ScopedDependency>();
        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();

        var instances = new List<ScopedDependency>();
        var method = new ScopedMethod("test.method", scopeFactory, svc =>
        {
            instances.Add(svc.GetRequiredService<ScopedDependency>());
            return new FakeMethod("test.method", null);
        });

        await method.ExecuteAsync(null);
        await method.ExecuteAsync(null);

        Assert.Equal(2, instances.Count);
        Assert.NotSame(instances[0], instances[1]);
    }

    private sealed class ScopedDependency { }

    private sealed class FakeMethod : IAdminMethod
    {
        public string Name { get; }
        private readonly object? _result;

        public FakeMethod(string name, object? result)
        {
            Name = name;
            _result = result;
        }

        public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
            => Task.FromResult(_result);
    }

    private sealed class CapturingMethod : IAdminMethod
    {
        public string Name { get; }
        private readonly Action<JsonElement?> _capture;

        public CapturingMethod(string name, Action<JsonElement?> capture)
        {
            Name = name;
            _capture = capture;
        }

        public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        {
            _capture(parameters);
            return Task.FromResult<object?>(null);
        }
    }
}
