namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Data.Repositories;

public sealed class TopicHistoryQueryMethod : IAdminMethod
{
    private readonly ITopicHistoryRepository _topicHistory;
    public string Name => "topichistory.query";
    public TopicHistoryQueryMethod(ITopicHistoryRepository topicHistory) => _topicHistory = topicHistory;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var channel = AdminParamHelper.RequireString(p, "channel");

        int limit = 20;
        if (p.TryGetProperty("limit", out var limitEl))
            limit = limitEl.GetInt32();

        var entries = await _topicHistory.GetByChannelAsync(channel, limit, ct);

        return new
        {
            topics = entries.Select(e => new
            {
                id = e.Id,
                channel = e.ChannelName,
                topic = e.Topic,
                set_by = e.SetBy,
                set_at = e.SetAt,
            }).ToList()
        };
    }
}
