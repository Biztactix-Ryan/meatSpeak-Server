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
        var p = AdminParamHelper.Require(parameters);
        var mask = AdminParamHelper.RequireString(p, "mask");
        var reason = AdminParamHelper.OptionalString(p, "reason");

        DateTimeOffset? expiresAt = null;
        if (p.TryGetProperty("duration", out var durationEl))
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
        var p = AdminParamHelper.Require(parameters);
        var id = AdminParamHelper.RequireGuid(p, "id");

        await _bans.RemoveAsync(id, ct);

        if (_audit != null)
        {
            await _audit.AddAsync(new AuditLogEntity
            {
                Action = "ban.remove",
                Actor = "admin-api",
                Target = id.ToString(),
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
        var p = AdminParamHelper.Require(parameters);
        var mask = AdminParamHelper.RequireString(p, "mask");

        var banned = await _bans.IsBannedAsync(mask, ct);
        return new { banned };
    }
}
