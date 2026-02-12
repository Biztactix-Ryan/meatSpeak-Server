namespace MeatSpeak.Server.Data.Repositories;

using MeatSpeak.Server.Data.Entities;

public interface IUserAccountRepository
{
    Task<UserAccountEntity?> GetByAccountAsync(string account, CancellationToken ct = default);
    Task AddAsync(UserAccountEntity entity, CancellationToken ct = default);
    Task UpdateLastLoginAsync(string account, DateTimeOffset timestamp, CancellationToken ct = default);
}
