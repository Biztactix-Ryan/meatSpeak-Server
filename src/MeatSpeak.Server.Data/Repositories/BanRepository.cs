namespace MeatSpeak.Server.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data.Entities;

public sealed class BanRepository : IBanRepository
{
    private readonly MeatSpeakDbContext _db;

    public BanRepository(MeatSpeakDbContext db) => _db = db;

    public async Task<IReadOnlyList<ServerBanEntity>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.ServerBans
            .Where(b => b.ExpiresAt == null || b.ExpiresAt > now)
            .OrderByDescending(b => b.SetAt)
            .ToListAsync(ct);
    }

    public async Task<ServerBanEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.ServerBans.FindAsync(new object[] { id }, ct);

    public async Task<bool> IsBannedAsync(string mask, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return await _db.ServerBans
            .AnyAsync(b => b.Mask == mask && (b.ExpiresAt == null || b.ExpiresAt > now), ct);
    }

    public async Task AddAsync(ServerBanEntity ban, CancellationToken ct = default)
    {
        _db.ServerBans.Add(ban);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveAsync(Guid id, CancellationToken ct = default)
    {
        var ban = await _db.ServerBans.FindAsync(new object[] { id }, ct);
        if (ban != null)
        {
            _db.ServerBans.Remove(ban);
            await _db.SaveChangesAsync(ct);
        }
    }
}
