namespace MeatSpeak.Server.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data.Entities;

public sealed class UserAccountRepository : IUserAccountRepository
{
    private readonly MeatSpeakDbContext _db;

    public UserAccountRepository(MeatSpeakDbContext db) => _db = db;

    public Task<UserAccountEntity?> GetByAccountAsync(string account, CancellationToken ct = default)
        => _db.UserAccounts.FirstOrDefaultAsync(
            a => a.Account == account && !a.Disabled, ct);

    public async Task AddAsync(UserAccountEntity entity, CancellationToken ct = default)
    {
        _db.UserAccounts.Add(entity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateLastLoginAsync(string account, DateTimeOffset timestamp, CancellationToken ct = default)
    {
        await _db.UserAccounts
            .Where(a => a.Account == account)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.LastLogin, timestamp), ct);
    }
}
