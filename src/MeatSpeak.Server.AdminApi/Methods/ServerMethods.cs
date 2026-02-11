namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Core.Server;

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
    public string Name => "server.motd.set";
    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { status = "ok" });
}

public sealed class ServerShutdownMethod : IAdminMethod
{
    public string Name => "server.shutdown";
    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { status = "shutting_down" });
}
