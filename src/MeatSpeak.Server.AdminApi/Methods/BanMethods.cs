namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;

public sealed class BanListMethod : IAdminMethod
{
    private readonly IBanRepository _bans;
    public string Name => "ban.list";
    public BanListMethod(IBanRepository bans) => _bans = bans;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var bans = await _bans.GetAllActiveAsync(ct);
        return new
        {
            bans = bans.Select(b => new
            {
                id = b.Id,
                mask = b.Mask,
                reason = b.Reason,
                set_by = b.SetBy,
                set_at = b.SetAt,
                expires_at = b.ExpiresAt,
            }).ToList()
        };
    }
}

public sealed class BanAddMethod : IAdminMethod
{
    private readonly IBanRepository _bans;
    private readonly IAuditLogRepository? _audit;
    public string Name => "ban.add";

    public BanAddMethod(IBanRepository bans, IAuditLogRepository? audit = null)
    {
        _bans = bans;
        _audit = audit;
    }

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var mask = parameters.Value.GetProperty("mask").GetString()
            ?? throw new JsonException("Missing 'mask'");

        string? reason = null;
        if (parameters.Value.TryGetProperty("reason", out var reasonEl))
            reason = reasonEl.GetString();

        DateTimeOffset? expiresAt = null;
        if (parameters.Value.TryGetProperty("duration", out var durationEl))
        {
            var seconds = durationEl.GetInt32();
            if (seconds > 0)
                expiresAt = DateTimeOffset.UtcNow.AddSeconds(seconds);
        }

        var ban = new ServerBanEntity
        {
            Id = Guid.NewGuid(),
            Mask = mask,
            Reason = reason,
            SetBy = "admin-api",
            SetAt = DateTimeOffset.UtcNow,
            ExpiresAt = expiresAt,
        };

        await _bans.AddAsync(ban, ct);

        if (_audit != null)
        {
            await _audit.AddAsync(new AuditLogEntity
            {
                Action = "ban.add",
                Actor = "admin-api",
                Target = mask,
                Details = reason,
            }, ct);
        }

        return new { status = "ok", id = ban.Id };
    }
}

public sealed class BanRemoveMethod : IAdminMethod
{
    private readonly IBanRepository _bans;
    private readonly IAuditLogRepository? _audit;
    public string Name => "ban.remove";

    public BanRemoveMethod(IBanRepository bans, IAuditLogRepository? audit = null)
    {
        _bans = bans;
        _audit = audit;
    }

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var idStr = parameters.Value.GetProperty("id").GetString()
            ?? throw new JsonException("Missing 'id'");

        if (!Guid.TryParse(idStr, out var id))
            throw new JsonException("Invalid 'id' format");

        await _bans.RemoveAsync(id, ct);

        if (_audit != null)
        {
            await _audit.AddAsync(new AuditLogEntity
            {
                Action = "ban.remove",
                Actor = "admin-api",
                Target = idStr,
            }, ct);
        }

        return new { status = "ok" };
    }
}

public sealed class BanCheckMethod : IAdminMethod
{
    private readonly IBanRepository _bans;
    public string Name => "ban.check";
    public BanCheckMethod(IBanRepository bans) => _bans = bans;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        if (parameters == null)
            throw new JsonException("Missing parameters");

        var mask = parameters.Value.GetProperty("mask").GetString()
            ?? throw new JsonException("Missing 'mask'");

        var banned = await _bans.IsBannedAsync(mask, ct);
        return new { banned };
    }
}
