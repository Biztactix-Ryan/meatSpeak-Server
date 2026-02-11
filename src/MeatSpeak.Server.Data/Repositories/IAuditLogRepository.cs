namespace MeatSpeak.Server.Data.Repositories;

using MeatSpeak.Server.Data.Entities;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntity entry, CancellationToken ct = default);
    Task<IReadOnlyList<AuditLogEntity>> QueryAsync(string? actor = null, string? action = null, int limit = 50, int offset = 0, CancellationToken ct = default);
}
