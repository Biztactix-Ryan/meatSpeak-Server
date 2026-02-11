namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Data.Repositories;

public sealed class UserHistoryQueryMethod : IAdminMethod
{
    private readonly IUserHistoryRepository _userHistory;
    public string Name => "userhistory.query";
    public UserHistoryQueryMethod(IUserHistoryRepository userHistory) => _userHistory = userHistory;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        string? nickname = null;
        string? account = null;
        int limit = 20;

        if (parameters != null)
        {
            if (parameters.Value.TryGetProperty("nickname", out var nickEl))
                nickname = nickEl.GetString();
            if (parameters.Value.TryGetProperty("account", out var accountEl))
                account = accountEl.GetString();
            if (parameters.Value.TryGetProperty("limit", out var limitEl))
                limit = limitEl.GetInt32();
        }

        if (nickname == null && account == null)
            throw new JsonException("At least one of 'nickname' or 'account' is required");

        var entries = nickname != null
            ? await _userHistory.GetByNicknameAsync(nickname, limit, ct)
            : await _userHistory.GetByAccountAsync(account!, limit, ct);

        return new
        {
            history = entries.Select(e => new
            {
                id = e.Id,
                nickname = e.Nickname,
                username = e.Username,
                hostname = e.Hostname,
                account = e.Account,
                connected_at = e.ConnectedAt,
                disconnected_at = e.DisconnectedAt,
                quit_reason = e.QuitReason,
            }).ToList()
        };
    }
}
