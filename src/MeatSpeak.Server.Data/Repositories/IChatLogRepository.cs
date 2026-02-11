namespace MeatSpeak.Server.Data.Repositories;

using MeatSpeak.Server.Data.Entities;

public interface IChatLogRepository
{
    Task AddAsync(ChatLogEntity entry, CancellationToken ct = default);
    Task<IReadOnlyList<ChatLogEntity>> GetByChannelAsync(string channelName, int limit = 50, int offset = 0, CancellationToken ct = default);
    Task<IReadOnlyList<ChatLogEntity>> GetPrivateMessagesAsync(string nick1, string nick2, int limit = 50, CancellationToken ct = default);
}
