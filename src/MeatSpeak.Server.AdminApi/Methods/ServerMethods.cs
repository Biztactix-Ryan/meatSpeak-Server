namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.AdminApi.Auth;
using MeatSpeak.Server.Core.Server;
using Microsoft.Extensions.Hosting;

public sealed class ServerStatsMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "server.stats";
    public ServerStatsMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        return Task.FromResult<object?>(new
        {
            connections = _server.ConnectionCount,
            channels = _server.ChannelCount,
            uptime = (DateTimeOffset.UtcNow - _server.StartedAt).TotalSeconds,
        });
    }
}

public sealed class ServerRehashMethod : IAdminMethod
{
    private readonly IConfigReloader? _reloader;
    public string Name => "server.rehash";
    public ServerRehashMethod(IConfigReloader? reloader = null) => _reloader = reloader;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        if (_reloader != null) await _reloader.ReloadAsync(ct);
        return new { status = "ok" };
    }
}

public sealed class ServerMotdGetMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "server.motd.get";
    public ServerMotdGetMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { motd = _server.Config.Motd });
}

public sealed class ServerMotdSetMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "server.motd.set";
    public ServerMotdSetMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var motd = parameters.Value.GetProperty("motd");
        var lines = new List<string>();
        foreach (var item in motd.EnumerateArray())
            lines.Add(item.GetString() ?? "");

        _server.Config.Motd = lines;
        return Task.FromResult<object?>(new { status = "ok" });
    }
}

public sealed class ServerShutdownMethod : IAdminMethod
{
    private readonly IHostApplicationLifetime _lifetime;
    public string Name => "server.shutdown";
    public ServerShutdownMethod(IHostApplicationLifetime lifetime) => _lifetime = lifetime;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        _lifetime.StopApplication();
        return Task.FromResult<object?>(new { status = "shutting_down" });
    }
}

public sealed class ServerOperSetMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "server.oper.set";
    public ServerOperSetMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var name = parameters.Value.GetProperty("name").GetString()
            ?? throw new JsonException("Missing 'name'");
        var password = parameters.Value.GetProperty("password").GetString()
            ?? throw new JsonException("Missing 'password'");

        _server.Config.OperName = name;
        _server.Config.OperPassword = PasswordHasher.HashPassword(password);
        return Task.FromResult<object?>(new { status = "ok" });
    }
}
