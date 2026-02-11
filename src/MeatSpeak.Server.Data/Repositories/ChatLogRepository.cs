namespace MeatSpeak.Server.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data.Entities;

public sealed class ChatLogRepository : IChatLogRepository
{
    private readonly MeatSpeakDbContext _db;

    public ChatLogRepository(MeatSpeakDbContext db) => _db = db;

    public async Task AddAsync(ChatLogEntity entry, CancellationToken ct = default)
    {
        _db.ChatLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ChatLogEntity>> GetByChannelAsync(string channelName, int limit = 50, int offset = 0, CancellationToken ct = default)
        => await _db.ChatLogs
            .Where(e => e.ChannelName == channelName)
            .OrderByDescending(e => e.SentAt)
            .Skip(offset)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ChatLogEntity>> GetPrivateMessagesAsync(string nick1, string nick2, int limit = 50, CancellationToken ct = default)
        => await _db.ChatLogs
            .Where(e => e.ChannelName == null &&
                ((e.Sender == nick1 && e.Target == nick2) ||
                 (e.Sender == nick2 && e.Target == nick1)))
            .OrderByDescending(e => e.SentAt)
            .Take(limit)
            .ToListAsync(ct);
}
