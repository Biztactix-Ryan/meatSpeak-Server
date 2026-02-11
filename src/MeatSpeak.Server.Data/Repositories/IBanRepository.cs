namespace MeatSpeak.Server.Data.Repositories;

using MeatSpeak.Server.Data.Entities;

public interface IBanRepository
{
    Task<IReadOnlyList<ServerBanEntity>> GetAllActiveAsync(CancellationToken ct = default);
    Task<ServerBanEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> IsBannedAsync(string mask, CancellationToken ct = default);
    Task AddAsync(ServerBanEntity ban, CancellationToken ct = default);
    Task RemoveAsync(Guid id, CancellationToken ct = default);
}
