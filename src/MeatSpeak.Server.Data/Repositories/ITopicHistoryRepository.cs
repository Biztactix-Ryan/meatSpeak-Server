namespace MeatSpeak.Server.Data.Repositories;

using MeatSpeak.Server.Data.Entities;

public interface ITopicHistoryRepository
{
    Task AddAsync(TopicHistoryEntity entry, CancellationToken ct = default);
    Task<IReadOnlyList<TopicHistoryEntity>> GetByChannelAsync(string channelName, int limit = 20, CancellationToken ct = default);
}
