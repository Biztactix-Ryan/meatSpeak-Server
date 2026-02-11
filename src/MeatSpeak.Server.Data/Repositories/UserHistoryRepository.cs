namespace MeatSpeak.Server.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data.Entities;

public sealed class UserHistoryRepository : IUserHistoryRepository
{
    private readonly MeatSpeakDbContext _db;

    public UserHistoryRepository(MeatSpeakDbContext db) => _db = db;

    public async Task AddAsync(UserHistoryEntity entry, CancellationToken ct = default)
    {
        _db.UserHistory.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateDisconnectAsync(long id, DateTimeOffset disconnectedAt, string? reason, CancellationToken ct = default)
    {
        var entry = await _db.UserHistory.FindAsync(new object[] { id }, ct);
        if (entry != null)
        {
            entry.DisconnectedAt = disconnectedAt;
            entry.QuitReason = reason;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<UserHistoryEntity>> GetByNicknameAsync(string nickname, int limit = 20, CancellationToken ct = default)
        => await _db.UserHistory
            .Where(e => e.Nickname == nickname)
            .OrderByDescending(e => e.ConnectedAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<UserHistoryEntity>> GetByAccountAsync(string account, int limit = 20, CancellationToken ct = default)
        => await _db.UserHistory
            .Where(e => e.Account == account)
            .OrderByDescending(e => e.ConnectedAt)
            .Take(limit)
            .ToListAsync(ct);
}
