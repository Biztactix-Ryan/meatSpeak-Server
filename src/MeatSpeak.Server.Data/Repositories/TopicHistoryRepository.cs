namespace MeatSpeak.Server.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data.Entities;

public sealed class TopicHistoryRepository : ITopicHistoryRepository
{
    private readonly MeatSpeakDbContext _db;

    public TopicHistoryRepository(MeatSpeakDbContext db) => _db = db;

    public async Task AddAsync(TopicHistoryEntity entry, CancellationToken ct = default)
    {
        _db.TopicHistory.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TopicHistoryEntity>> GetByChannelAsync(string channelName, int limit = 20, CancellationToken ct = default)
        => await _db.TopicHistory
            .Where(e => e.ChannelName == channelName)
            .OrderByDescending(e => e.SetAt)
            .Take(limit)
            .ToListAsync(ct);
}
