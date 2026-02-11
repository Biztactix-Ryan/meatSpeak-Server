namespace MeatSpeak.Server.Data.Repositories;

using MeatSpeak.Server.Data.Entities;

public interface IChannelRepository
{
    Task<ChannelEntity?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<ChannelEntity>> GetAllAsync(CancellationToken ct = default);
    Task UpsertAsync(ChannelEntity channel, CancellationToken ct = default);
    Task DeleteAsync(string name, CancellationToken ct = default);
}
