namespace MeatSpeak.Server.Handlers;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;

public sealed class ChatHistoryHandler : ICommandHandler
{
    private readonly IServer _server;
    private readonly IServiceScopeFactory _scopeFactory;
    private const int MaxLimit = 100;

    public string Command => IrcConstants.CHATHISTORY;
    public SessionState MinimumState => SessionState.Registered;

    public ChatHistoryHandler(IServer server, IServiceScopeFactory scopeFactory)
    {
        _server = server;
        _scopeFactory = scopeFactory;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (!CapHelper.HasCap(session, "draft/chathistory"))
        {
            await SendFail(session, "*", "NEED_CAPABILITY", "draft/chathistory capability required");
            return;
        }

        if (message.Parameters.Count < 1)
        {
            await SendFail(session, "*", "NEED_MORE_PARAMS", "Not enough parameters");
            return;
        }

        var subcommand = message.GetParam(0)!.ToUpperInvariant();

        switch (subcommand)
        {
            case "LATEST":
                await HandleLatest(session, message, ct);
                break;
            case "BEFORE":
                await HandleBefore(session, message, ct);
                break;
            case "AFTER":
                await HandleAfter(session, message, ct);
                break;
            case "AROUND":
                await HandleAround(session, message, ct);
                break;
            case "BETWEEN":
                await HandleBetween(session, message, ct);
                break;
            case "TARGETS":
                await HandleTargets(session, message, ct);
                break;
            default:
                await SendFail(session, subcommand, "UNKNOWN_COMMAND", $"Unknown CHATHISTORY subcommand: {subcommand}");
                break;
        }
    }

    // CHATHISTORY LATEST <target> <reference> <limit>
    private async ValueTask HandleLatest(ISession session, IrcMessage message, CancellationToken ct)
    {
        if (message.Parameters.Count < 4)
        {
            await SendFail(session, "LATEST", "NEED_MORE_PARAMS", "Usage: CHATHISTORY LATEST <target> <reference> <limit>");
            return;
        }

        var target = message.GetParam(1)!;
        var reference = message.GetParam(2)!;
        if (!TryParseLimit(message.GetParam(3)!, out var limit))
        {
            await SendFail(session, "LATEST", "INVALID_PARAMS", "Invalid limit");
            return;
        }

        if (!await ValidateTarget(session, "LATEST", target))
            return;

        var requester = session.Info.Nickname!;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IChatLogRepository>();

        IReadOnlyList<ChatLogEntity> messages;
        if (reference == "*")
        {
            messages = await repo.GetLatestAsync(target, requester, limit, ct);
        }
        else
        {
            var refTime = await ResolveReference(repo, reference, ct);
            if (refTime == null)
            {
                await SendFail(session, "LATEST", "INVALID_PARAMS", "Cannot resolve reference");
                return;
            }
            // LATEST with a reference returns messages after that reference (most recent up to limit)
            messages = await repo.GetAfterAsync(target, requester, refTime.Value, limit, ct);
        }

        await SendBatch(session, target, messages);
    }

    // CHATHISTORY BEFORE <target> <reference> <limit>
    private async ValueTask HandleBefore(ISession session, IrcMessage message, CancellationToken ct)
    {
        if (message.Parameters.Count < 4)
        {
            await SendFail(session, "BEFORE", "NEED_MORE_PARAMS", "Usage: CHATHISTORY BEFORE <target> <reference> <limit>");
            return;
        }

        var target = message.GetParam(1)!;
        var reference = message.GetParam(2)!;
        if (!TryParseLimit(message.GetParam(3)!, out var limit))
        {
            await SendFail(session, "BEFORE", "INVALID_PARAMS", "Invalid limit");
            return;
        }

        if (!await ValidateTarget(session, "BEFORE", target))
            return;

        var requester = session.Info.Nickname!;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IChatLogRepository>();

        var refTime = await ResolveReference(repo, reference, ct);
        if (refTime == null)
        {
            await SendFail(session, "BEFORE", "INVALID_PARAMS", "Cannot resolve reference");
            return;
        }

        var messages = await repo.GetBeforeAsync(target, requester, refTime.Value, limit, ct);
        await SendBatch(session, target, messages);
    }

    // CHATHISTORY AFTER <target> <reference> <limit>
    private async ValueTask HandleAfter(ISession session, IrcMessage message, CancellationToken ct)
    {
        if (message.Parameters.Count < 4)
        {
            await SendFail(session, "AFTER", "NEED_MORE_PARAMS", "Usage: CHATHISTORY AFTER <target> <reference> <limit>");
            return;
        }

        var target = message.GetParam(1)!;
        var reference = message.GetParam(2)!;
        if (!TryParseLimit(message.GetParam(3)!, out var limit))
        {
            await SendFail(session, "AFTER", "INVALID_PARAMS", "Invalid limit");
            return;
        }

        if (!await ValidateTarget(session, "AFTER", target))
            return;

        var requester = session.Info.Nickname!;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IChatLogRepository>();

        var refTime = await ResolveReference(repo, reference, ct);
        if (refTime == null)
        {
            await SendFail(session, "AFTER", "INVALID_PARAMS", "Cannot resolve reference");
            return;
        }

        var messages = await repo.GetAfterAsync(target, requester, refTime.Value, limit, ct);
        await SendBatch(session, target, messages);
    }

    // CHATHISTORY AROUND <target> <reference> <limit>
    private async ValueTask HandleAround(ISession session, IrcMessage message, CancellationToken ct)
    {
        if (message.Parameters.Count < 4)
        {
            await SendFail(session, "AROUND", "NEED_MORE_PARAMS", "Usage: CHATHISTORY AROUND <target> <reference> <limit>");
            return;
        }

        var target = message.GetParam(1)!;
        var reference = message.GetParam(2)!;
        if (!TryParseLimit(message.GetParam(3)!, out var limit))
        {
            await SendFail(session, "AROUND", "INVALID_PARAMS", "Invalid limit");
            return;
        }

        if (!await ValidateTarget(session, "AROUND", target))
            return;

        var requester = session.Info.Nickname!;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IChatLogRepository>();

        var refTime = await ResolveReference(repo, reference, ct);
        if (refTime == null)
        {
            await SendFail(session, "AROUND", "INVALID_PARAMS", "Cannot resolve reference");
            return;
        }

        var messages = await repo.GetAroundAsync(target, requester, refTime.Value, limit, ct);
        await SendBatch(session, target, messages);
    }

    // CHATHISTORY BETWEEN <target> <ref_from> <ref_to> <limit>
    private async ValueTask HandleBetween(ISession session, IrcMessage message, CancellationToken ct)
    {
        if (message.Parameters.Count < 5)
        {
            await SendFail(session, "BETWEEN", "NEED_MORE_PARAMS", "Usage: CHATHISTORY BETWEEN <target> <ref_from> <ref_to> <limit>");
            return;
        }

        var target = message.GetParam(1)!;
        var refFrom = message.GetParam(2)!;
        var refTo = message.GetParam(3)!;
        if (!TryParseLimit(message.GetParam(4)!, out var limit))
        {
            await SendFail(session, "BETWEEN", "INVALID_PARAMS", "Invalid limit");
            return;
        }

        if (!await ValidateTarget(session, "BETWEEN", target))
            return;

        var requester = session.Info.Nickname!;
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IChatLogRepository>();

        var from = await ResolveReference(repo, refFrom, ct);
        var to = await ResolveReference(repo, refTo, ct);
        if (from == null || to == null)
        {
            await SendFail(session, "BETWEEN", "INVALID_PARAMS", "Cannot resolve reference");
            return;
        }

        var messages = await repo.GetBetweenAsync(target, requester, from.Value, to.Value, limit, ct);
        await SendBatch(session, target, messages);
    }

    // CHATHISTORY TARGETS <from> <to> <limit>
    private async ValueTask HandleTargets(ISession session, IrcMessage message, CancellationToken ct)
    {
        if (message.Parameters.Count < 4)
        {
            await SendFail(session, "TARGETS", "NEED_MORE_PARAMS", "Usage: CHATHISTORY TARGETS <from> <to> <limit>");
            return;
        }

        var refFrom = message.GetParam(1)!;
        var refTo = message.GetParam(2)!;
        if (!TryParseLimit(message.GetParam(3)!, out var limit))
        {
            await SendFail(session, "TARGETS", "INVALID_PARAMS", "Invalid limit");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IChatLogRepository>();

        var from = await ResolveReference(repo, refFrom, ct);
        var to = await ResolveReference(repo, refTo, ct);
        if (from == null || to == null)
        {
            await SendFail(session, "TARGETS", "INVALID_PARAMS", "Cannot resolve reference");
            return;
        }

        var requester = session.Info.Nickname!;
        var allTargets = await repo.GetTargetsAsync(requester, from.Value, to.Value, limit, ct);

        // Filter channel targets to channels the user is currently a member of
        var targets = allTargets
            .Where(t => !t.Target.StartsWith('#') ||
                (_server.Channels.TryGetValue(t.Target, out var ch) && ch.IsMember(requester)))
            .ToList();

        var batchRef = BatchHelper.GenerateReference();
        await BatchHelper.StartBatch(session, batchRef, "draft/chathistory-targets");

        foreach (var (target, latestAt) in targets)
        {
            var batchTag = CapHelper.HasCap(session, "batch") ? $"batch={batchRef}" : null;
            var tags = CapHelper.BuildTags(session, null, batchTag);
            var timestamp = $"{latestAt.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}";
            if (tags != null)
                await session.SendTaggedMessageAsync(tags, _server.Config.ServerName, "CHATHISTORY", "TARGETS", target, timestamp);
            else
                await session.SendMessageAsync(_server.Config.ServerName, "CHATHISTORY", "TARGETS", target, timestamp);
        }

        await BatchHelper.EndBatch(session, batchRef);
    }

    private static readonly HashSet<string> EventTypes = new(StringComparer.OrdinalIgnoreCase)
        { "JOIN", "PART", "QUIT", "TOPIC", "KICK" };

    private async ValueTask SendBatch(ISession session, string target, IReadOnlyList<ChatLogEntity> messages)
    {
        var batchRef = BatchHelper.GenerateReference();
        await BatchHelper.StartBatch(session, batchRef, "chathistory", target);

        var hasEventPlayback = CapHelper.HasCap(session, "draft/event-playback");

        // Messages from GetLatest/GetBefore come in descending order; reverse for chronological
        var ordered = messages.OrderBy(m => m.SentAt).ToList();

        foreach (var msg in ordered)
        {
            var isEvent = EventTypes.Contains(msg.MessageType);

            // Skip events for clients without event-playback cap
            if (isEvent && !hasEventPlayback)
                continue;

            var batchTag = CapHelper.HasCap(session, "batch") ? $"batch={batchRef}" : null;

            // Build tags with original time and msgid
            var parts = new List<string>();
            parts.Add($"time={msg.SentAt.UtcDateTime:yyyy-MM-dd'T'HH:mm:ss.fff'Z'}");
            if (msg.MsgId != null && CapHelper.HasCap(session, "message-tags"))
                parts.Add($"msgid={msg.MsgId}");
            if (batchTag != null)
                parts.Add(batchTag);
            var tags = string.Join(';', parts);

            var prefix = msg.Sender;
            var msgTarget = msg.ChannelName ?? msg.Target ?? target;

            if (isEvent)
            {
                // Replay events with their original command and parameter structure
                switch (msg.MessageType.ToUpperInvariant())
                {
                    case "JOIN":
                        await session.SendTaggedMessageAsync(tags, prefix, IrcConstants.JOIN, msgTarget);
                        break;
                    case "PART":
                        if (!string.IsNullOrEmpty(msg.Message))
                            await session.SendTaggedMessageAsync(tags, prefix, IrcConstants.PART, msgTarget, msg.Message);
                        else
                            await session.SendTaggedMessageAsync(tags, prefix, IrcConstants.PART, msgTarget);
                        break;
                    case "QUIT":
                        await session.SendTaggedMessageAsync(tags, prefix, IrcConstants.QUIT, msg.Message);
                        break;
                    case "TOPIC":
                        await session.SendTaggedMessageAsync(tags, prefix, IrcConstants.TOPIC, msgTarget, msg.Message);
                        break;
                    case "KICK":
                        // Message stored as "targetNick reason"
                        var spaceIdx = msg.Message.IndexOf(' ');
                        if (spaceIdx > 0)
                            await session.SendTaggedMessageAsync(tags, prefix, IrcConstants.KICK, msgTarget, msg.Message[..spaceIdx], msg.Message[(spaceIdx + 1)..]);
                        else
                            await session.SendTaggedMessageAsync(tags, prefix, IrcConstants.KICK, msgTarget, msg.Message);
                        break;
                }
            }
            else
            {
                var command = msg.MessageType == "NOTICE" ? IrcConstants.NOTICE : IrcConstants.PRIVMSG;
                await session.SendTaggedMessageAsync(tags, prefix, command, msgTarget, msg.Message);
            }
        }

        await BatchHelper.EndBatch(session, batchRef);
    }

    private static async Task<DateTimeOffset?> ResolveReference(IChatLogRepository repo, string reference, CancellationToken ct)
    {
        if (reference.StartsWith("timestamp=", StringComparison.OrdinalIgnoreCase))
        {
            var tsStr = reference[10..];
            if (DateTimeOffset.TryParse(tsStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var ts))
                return ts;
            return null;
        }

        if (reference.StartsWith("msgid=", StringComparison.OrdinalIgnoreCase))
        {
            var msgId = reference[6..];
            var msg = await repo.GetByMsgIdAsync(msgId, ct);
            return msg?.SentAt;
        }

        // Try as raw timestamp
        if (DateTimeOffset.TryParse(reference, null, System.Globalization.DateTimeStyles.RoundtripKind, out var directTs))
            return directTs;

        return null;
    }

    private async ValueTask<bool> ValidateTarget(ISession session, string subcommand, string target)
    {
        if (target.StartsWith('#'))
        {
            if (!_server.Channels.TryGetValue(target, out var channel) || !channel.IsMember(session.Info.Nickname!))
            {
                await SendFail(session, subcommand, "INVALID_TARGET", "Messages could not be retrieved");
                return false;
            }
        }
        // PM targets are always allowed (the repo scopes queries to the requester)
        return true;
    }

    private static bool TryParseLimit(string limitStr, out int limit)
    {
        if (int.TryParse(limitStr, out limit) && limit > 0)
        {
            limit = Math.Min(limit, MaxLimit);
            return true;
        }
        limit = 0;
        return false;
    }

    private ValueTask SendFail(ISession session, string subcommand, string code, string description)
        => session.SendMessageAsync(_server.Config.ServerName, "FAIL", "CHATHISTORY", code, subcommand, description);
}
