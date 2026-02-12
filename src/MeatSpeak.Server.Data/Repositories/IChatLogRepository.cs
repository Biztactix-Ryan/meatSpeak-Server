namespace MeatSpeak.Server.Data.Repositories;

using MeatSpeak.Server.Data.Entities;

public interface IChatLogRepository
{
    Task AddAsync(ChatLogEntity entry, CancellationToken ct = default);
    Task<IReadOnlyList<ChatLogEntity>> GetByChannelAsync(string channelName, int limit = 50, int offset = 0, CancellationToken ct = default);
    Task<IReadOnlyList<ChatLogEntity>> GetPrivateMessagesAsync(string nick1, string nick2, int limit = 50, CancellationToken ct = default);

    // Chat history methods (requester is used to scope PM queries to the requesting user)
    Task<IReadOnlyList<ChatLogEntity>> GetBeforeAsync(string target, string requester, DateTimeOffset before, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<ChatLogEntity>> GetAfterAsync(string target, string requester, DateTimeOffset after, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<ChatLogEntity>> GetAroundAsync(string target, string requester, DateTimeOffset around, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<ChatLogEntity>> GetBetweenAsync(string target, string requester, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<ChatLogEntity>> GetLatestAsync(string target, string requester, int limit, CancellationToken ct = default);
    Task<ChatLogEntity?> GetByMsgIdAsync(string msgId, CancellationToken ct = default);
    Task<IReadOnlyList<(string Target, DateTimeOffset LatestMessageAt)>> GetTargetsAsync(string requester, DateTimeOffset from, DateTimeOffset to, int limit, CancellationToken ct = default);
    Task RedactByMsgIdAsync(string msgId, string redactedBy, CancellationToken ct = default);
}
