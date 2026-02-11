namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.Data.Repositories;

public sealed class ChatLogQueryMethod : IAdminMethod
{
    private readonly IChatLogRepository _chatLogs;
    public string Name => "chatlog.query";
    public ChatLogQueryMethod(IChatLogRepository chatLogs) => _chatLogs = chatLogs;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        string? channel = null;
        string? sender = null;
        int limit = 50;
        int offset = 0;

        if (parameters != null)
        {
            if (parameters.Value.TryGetProperty("channel", out var channelEl))
                channel = channelEl.GetString();
            if (parameters.Value.TryGetProperty("sender", out var senderEl))
                sender = senderEl.GetString();
            if (parameters.Value.TryGetProperty("limit", out var limitEl))
                limit = limitEl.GetInt32();
            if (parameters.Value.TryGetProperty("offset", out var offsetEl))
                offset = offsetEl.GetInt32();
        }

        if (channel == null && sender == null)
            throw new JsonException("At least one of 'channel' or 'sender' is required");

        var entries = await _chatLogs.GetByChannelAsync(channel ?? string.Empty, limit, offset, ct);

        // If filtering by sender, apply in-memory filter
        if (sender != null)
        {
            entries = entries.Where(e => string.Equals(e.Sender, sender, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return new
        {
            messages = entries.Select(e => new
            {
                id = e.Id,
                channel = e.ChannelName,
                target = e.Target,
                sender = e.Sender,
                message = e.Message,
                type = e.MessageType,
                sent_at = e.SentAt,
            }).ToList()
        };
    }
}
