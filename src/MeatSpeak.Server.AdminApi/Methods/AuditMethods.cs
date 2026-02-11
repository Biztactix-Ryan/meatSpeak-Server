namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Data.Repositories;

public sealed class AuditQueryMethod : IAdminMethod
{
    private readonly IAuditLogRepository _audit;
    public string Name => "audit.query";
    public AuditQueryMethod(IAuditLogRepository audit) => _audit = audit;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        string? actor = null;
        string? action = null;
        int limit = 50;
        int offset = 0;

        if (parameters != null)
        {
            if (parameters.Value.TryGetProperty("actor", out var actorEl))
                actor = actorEl.GetString();
            if (parameters.Value.TryGetProperty("action", out var actionEl))
                action = actionEl.GetString();
            if (parameters.Value.TryGetProperty("limit", out var limitEl))
                limit = limitEl.GetInt32();
            if (parameters.Value.TryGetProperty("offset", out var offsetEl))
                offset = offsetEl.GetInt32();
        }

        var entries = await _audit.QueryAsync(actor, action, limit, offset, ct);
        return new
        {
            entries = entries.Select(e => new
            {
                id = e.Id,
                action = e.Action,
                actor = e.Actor,
                target = e.Target,
                details = e.Details,
                timestamp = e.Timestamp,
            }).ToList()
        };
    }
}
