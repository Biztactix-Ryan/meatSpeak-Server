namespace MeatSpeak.Server.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MeatSpeak.Server.Data.Entities;

public sealed class DbWriteService : BackgroundService
{
    private readonly DbWriteQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DbWriteService> _logger;
    private const int MaxBatchSize = 100;

    public DbWriteService(DbWriteQueue queue, IServiceScopeFactory scopeFactory, ILogger<DbWriteService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DbWriteService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Wait for at least one item
                if (!await _queue.Reader.WaitToReadAsync(stoppingToken))
                    break;

                await DrainBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DbWriteService batch processing");
                await Task.Delay(100, stoppingToken);
            }
        }

        // Flush remaining items on shutdown
        try
        {
            _logger.LogInformation("DbWriteService shutting down, flushing remaining writes");
            while (_queue.Reader.TryRead(out _) || await _queue.Reader.WaitToReadAsync(new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token))
            {
                await DrainBatchAsync(CancellationToken.None);
                if (!_queue.Reader.TryPeek(out _))
                    break;
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error flushing remaining writes during shutdown");
        }

        _logger.LogInformation("DbWriteService stopped");
    }

    private async Task DrainBatchAsync(CancellationToken ct)
    {
        var batch = new List<DbWriteItem>(MaxBatchSize);
        while (batch.Count < MaxBatchSize && _queue.Reader.TryRead(out var item))
            batch.Add(item);

        if (batch.Count == 0)
            return;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeatSpeakDbContext>();

        foreach (var item in batch)
        {
            switch (item)
            {
                case AddChatLog log:
                    db.ChatLogs.Add(log.Entity);
                    break;

                case AddUserHistory uh:
                    db.UserHistory.Add(uh.Entity);
                    break;

                case UpdateUserDisconnect ud:
                    var entry = await db.UserHistory
                        .Where(e => e.Nickname == ud.Nickname && e.DisconnectedAt == null)
                        .OrderByDescending(e => e.ConnectedAt)
                        .FirstOrDefaultAsync(ct);
                    if (entry != null)
                    {
                        entry.DisconnectedAt = ud.DisconnectedAt;
                        entry.QuitReason = ud.Reason;
                    }
                    break;

                case UpsertChannel uc:
                    var existing = await db.Channels
                        .FirstOrDefaultAsync(c => c.Name == uc.Entity.Name, ct);
                    if (existing != null)
                    {
                        existing.Topic = uc.Entity.Topic;
                        existing.TopicSetBy = uc.Entity.TopicSetBy;
                        existing.TopicSetAt = uc.Entity.TopicSetAt;
                        existing.Key = uc.Entity.Key;
                        existing.UserLimit = uc.Entity.UserLimit;
                        existing.Modes = uc.Entity.Modes;
                    }
                    else
                    {
                        db.Channels.Add(uc.Entity);
                    }
                    break;

                case DeleteChannel dc:
                    var toDelete = await db.Channels
                        .FirstOrDefaultAsync(c => c.Name == dc.Name, ct);
                    if (toDelete != null)
                        db.Channels.Remove(toDelete);
                    break;

                case AddTopicHistory th:
                    db.TopicHistory.Add(th.Entity);
                    break;

                case RedactChatLog redact:
                    var chatEntry = await db.ChatLogs
                        .FirstOrDefaultAsync(e => e.MsgId == redact.MsgId, ct);
                    if (chatEntry != null)
                    {
                        chatEntry.IsRedacted = true;
                        chatEntry.RedactedBy = redact.RedactedBy;
                        chatEntry.RedactedAt = DateTimeOffset.UtcNow;
                    }
                    break;
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
