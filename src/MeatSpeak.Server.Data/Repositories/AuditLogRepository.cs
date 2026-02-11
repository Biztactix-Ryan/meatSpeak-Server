namespace MeatSpeak.Server.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data.Entities;

public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly MeatSpeakDbContext _db;

    public AuditLogRepository(MeatSpeakDbContext db) => _db = db;

    public async Task AddAsync(AuditLogEntity entry, CancellationToken ct = default)
    {
        _db.AuditLog.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntity>> QueryAsync(string? actor = null, string? action = null, int limit = 50, int offset = 0, CancellationToken ct = default)
    {
        var query = _db.AuditLog.AsQueryable();
        if (actor != null) query = query.Where(e => e.Actor == actor);
        if (action != null) query = query.Where(e => e.Action == action);
        return await query.OrderByDescending(e => e.Timestamp).Skip(offset).Take(limit).ToListAsync(ct);
    }
}
