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

    /// <summary>
    /// Builds a base query for a target, scoped to the requesting user for PM queries.
    /// Channel queries return all non-redacted messages for that channel.
    /// PM queries only return messages between requester and target.
    /// </summary>
    private IQueryable<ChatLogEntity> TargetQuery(string target, string requester)
    {
        if (target.StartsWith('#'))
            return _db.ChatLogs.Where(e => e.ChannelName == target && !e.IsRedacted);
        return _db.ChatLogs.Where(e => e.ChannelName == null && !e.IsRedacted &&
            ((e.Sender == target && e.Target == requester) ||
             (e.Sender == requester && e.Target == target)));
    }

    public async Task<IReadOnlyList<ChatLogEntity>> GetBeforeAsync(string target, string requester, DateTimeOffset before, int limit, CancellationToken ct = default)
        => await TargetQuery(target, requester)
            .Where(e => e.SentAt < before)
            .OrderByDescending(e => e.SentAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ChatLogEntity>> GetAfterAsync(string target, string requester, DateTimeOffset after, int limit, CancellationToken ct = default)
        => await TargetQuery(target, requester)
            .Where(e => e.SentAt > after)
            .OrderBy(e => e.SentAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ChatLogEntity>> GetAroundAsync(string target, string requester, DateTimeOffset around, int limit, CancellationToken ct = default)
    {
        var half = limit / 2;
        var before = await TargetQuery(target, requester)
            .Where(e => e.SentAt <= around)
            .OrderByDescending(e => e.SentAt)
            .Take(half)
            .ToListAsync(ct);

        var after = await TargetQuery(target, requester)
            .Where(e => e.SentAt > around)
            .OrderBy(e => e.SentAt)
            .Take(limit - half)
            .ToListAsync(ct);

        var result = new List<ChatLogEntity>(before.Count + after.Count);
        before.Reverse();
        result.AddRange(before);
        result.AddRange(after);
        return result;
    }

    public async Task<IReadOnlyList<ChatLogEntity>> GetBetweenAsync(string target, string requester, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct = default)
        => await TargetQuery(target, requester)
            .Where(e => e.SentAt >= from && e.SentAt < to)
            .OrderBy(e => e.SentAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<ChatLogEntity>> GetLatestAsync(string target, string requester, int limit, CancellationToken ct = default)
        => await TargetQuery(target, requester)
            .OrderByDescending(e => e.SentAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<ChatLogEntity?> GetByMsgIdAsync(string msgId, CancellationToken ct = default)
        => await _db.ChatLogs.FirstOrDefaultAsync(e => e.MsgId == msgId, ct);

    public async Task<IReadOnlyList<(string Target, DateTimeOffset LatestMessageAt)>> GetTargetsAsync(string requester, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct = default)
    {
        // Channel messages: all channels with activity in the time range
        var channelResults = await _db.ChatLogs
            .Where(e => e.SentAt >= from && e.SentAt < to && !e.IsRedacted && e.ChannelName != null)
            .GroupBy(e => e.ChannelName!)
            .Select(g => new { Target = g.Key, LatestMessageAt = g.Max(e => e.SentAt) })
            .ToListAsync(ct);

        // PM messages: only conversations involving the requester, grouped by the other person
        var pmResults = await _db.ChatLogs
            .Where(e => e.SentAt >= from && e.SentAt < to && !e.IsRedacted && e.ChannelName == null &&
                (e.Sender == requester || e.Target == requester))
            .GroupBy(e => e.Sender == requester ? e.Target! : e.Sender)
            .Select(g => new { Target = g.Key, LatestMessageAt = g.Max(e => e.SentAt) })
            .ToListAsync(ct);

        return channelResults.Concat(pmResults)
            .OrderByDescending(r => r.LatestMessageAt)
            .Take(limit)
            .Select(r => (r.Target, r.LatestMessageAt))
            .ToList();
    }

    public async Task RedactByMsgIdAsync(string msgId, string redactedBy, CancellationToken ct = default)
    {
        var entry = await _db.ChatLogs.FirstOrDefaultAsync(e => e.MsgId == msgId, ct);
        if (entry != null)
        {
            entry.IsRedacted = true;
            entry.RedactedBy = redactedBy;
            entry.RedactedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
