namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Core.Server;

public sealed class UserListMethod : IAdminMethod
{
    private readonly IServer _server;
    public string Name => "user.list";
    public UserListMethod(IServer server) => _server = server;

    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var users = _server.Sessions.Values.Select(s => new
        {
            id = s.Id,
            nick = s.Info.Nickname,
            account = s.Info.Account,
            state = s.State.ToString(),
        }).ToList();
        return Task.FromResult<object?>(new { users });
    }
}

public sealed class UserInfoMethod : IAdminMethod
{
    public string Name => "user.info";
    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { status = "not_implemented" });
}

public sealed class UserKickMethod : IAdminMethod
{
    public string Name => "user.kick";
    public Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
        => Task.FromResult<object?>(new { status = "not_implemented" });
}
