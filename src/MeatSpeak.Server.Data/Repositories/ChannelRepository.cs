namespace MeatSpeak.Server.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data.Entities;

public sealed class ChannelRepository : IChannelRepository
{
    private readonly MeatSpeakDbContext _db;

    public ChannelRepository(MeatSpeakDbContext db) => _db = db;

    public async Task<ChannelEntity?> GetByNameAsync(string name, CancellationToken ct = default)
        => await _db.Channels.FirstOrDefaultAsync(c => c.Name == name, ct);

    public async Task<IReadOnlyList<ChannelEntity>> GetAllAsync(CancellationToken ct = default)
        => await _db.Channels.OrderBy(c => c.Name).ToListAsync(ct);

    public async Task UpsertAsync(ChannelEntity channel, CancellationToken ct = default)
    {
        var existing = await _db.Channels.FirstOrDefaultAsync(c => c.Name == channel.Name, ct);
        if (existing == null)
        {
            _db.Channels.Add(channel);
        }
        else
        {
            existing.Topic = channel.Topic;
            existing.TopicSetBy = channel.TopicSetBy;
            existing.TopicSetAt = channel.TopicSetAt;
            existing.Key = channel.Key;
            existing.UserLimit = channel.UserLimit;
            existing.Modes = channel.Modes;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string name, CancellationToken ct = default)
    {
        var channel = await _db.Channels.FirstOrDefaultAsync(c => c.Name == name, ct);
        if (channel != null)
        {
            _db.Channels.Remove(channel);
            await _db.SaveChangesAsync(ct);
        }
    }
}
