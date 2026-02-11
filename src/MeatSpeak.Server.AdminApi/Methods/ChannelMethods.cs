namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Core.Server;

public sealed class ChannelListMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "channel.list";
    public ChannelListMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var channels = _server.Channels.Values.Select(c => new
        {
            name = c.Name,
            members = c.Members.Count,
            topic = c.Topic,
        }).ToList();
        return Task.FromResult<object?>(new { channels });
    }
}

public sealed class ChannelInfoMethod : IAdminMethod
{
    public string Name => "channel.info";
    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { status = "not_implemented" });
}

public sealed class ChannelModeMethod : IAdminMethod
{
    public string Name => "channel.mode";
    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { status = "not_implemented" });
}

public sealed class ChannelTopicMethod : IAdminMethod
{
    public string Name => "channel.topic";
    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { status = "not_implemented" });
}

public sealed class ChannelCreateMethod : IAdminMethod
{
    public string Name => "channel.create";
    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { status = "not_implemented" });
}

public sealed class ChannelDeleteMethod : IAdminMethod
{
    public string Name => "channel.delete";
    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { status = "not_implemented" });
}
