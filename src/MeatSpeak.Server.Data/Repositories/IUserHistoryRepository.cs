namespace MeatSpeak.Server.Data.Repositories;

using MeatSpeak.Server.Data.Entities;

public interface IUserHistoryRepository
{
    Task AddAsync(UserHistoryEntity entry, CancellationToken ct = default);
    Task UpdateDisconnectAsync(long id, DateTimeOffset disconnectedAt, string? reason, CancellationToken ct = default);
    Task<IReadOnlyList<UserHistoryEntity>> GetByNicknameAsync(string nickname, int limit = 20, CancellationToken ct = default);
    Task<IReadOnlyList<UserHistoryEntity>> GetByAccountAsync(string account, int limit = 20, CancellationToken ct = default);
}
