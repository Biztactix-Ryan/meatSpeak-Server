namespace MeatSpeak.Server;

using System.Collections.Concurrent;
using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.State;
using MeatSpeak.Server.Transport;
using Microsoft.Extensions.Logging;
using IrcNumerics = MeatSpeak.Protocol.Numerics;

public sealed class IrcConnectionHandler : IConnectionHandler
{
    private readonly IServer _server;
    private readonly ILogger<IrcConnectionHandler> _logger;
    private readonly ServerMetrics _metrics;
    private readonly DbWriteQueue? _writeQueue;
    private readonly Dictionary<string, SessionImpl> _connectionSessions = new();
    private readonly ConcurrentDictionary<string, int> _ipConnectionCounts = new();
    private readonly object _sessionsLock = new();

    public IrcConnectionHandler(IServer server, ILogger<IrcConnectionHandler> logger, ServerMetrics metrics, DbWriteQueue? writeQueue = null)
    {
        _server = server;
        _logger = logger;
        _metrics = metrics;
        _writeQueue = writeQueue;
    }

    public void OnConnected(IConnection connection)
    {
        // Enforce global connection limit
        if (_server.ConnectionCount >= _server.Config.MaxConnections)
        {
            _logger.LogWarning("Max connections ({Max}) reached, rejecting {Remote}",
                _server.Config.MaxConnections, connection.RemoteEndPoint);
            connection.Disconnect();
            return;
        }

        // Enforce per-IP connection limit (exempt IPs bypass this)
        var ip = connection.RemoteEndPoint?.ToString()?.Split(':')[0] ?? "unknown";
        var isExempt = _server.Config.ExemptIps.Contains(ip);
        if (!isExempt)
        {
            var ipCount = _ipConnectionCounts.AddOrUpdate(ip, 1, (_, count) => count + 1);
            if (ipCount > _server.Config.MaxConnectionsPerIp)
            {
                _ipConnectionCounts.AddOrUpdate(ip, 0, (_, count) => Math.Max(0, count - 1));
                _logger.LogWarning("Per-IP limit ({Max}) exceeded for {Ip}, rejecting connection",
                    _server.Config.MaxConnectionsPerIp, ip);
                connection.Disconnect();
                return;
            }
        }

        var session = new SessionImpl(connection, _server.Config.ServerName);
        lock (_sessionsLock)
            _connectionSessions[connection.Id] = session;
        var floodConfig = _server.Config.Flood;
        if (floodConfig.Enabled && !isExempt)
        {
            session.Info.FloodLimiter = new FloodLimiter(
                floodConfig.BurstLimit,
                floodConfig.TokenIntervalSeconds,
                floodConfig.ExcessFloodThreshold);
        }
        _server.AddSession(session);
        session.StartCommandProcessing();
        _metrics.ConnectionAccepted();
        _metrics.ConnectionActive();
        _server.Events.Publish(new SessionConnectedEvent(session.Id));
        _logger.LogInformation("Client connected: {Id} from {Remote}", session.Id, connection.RemoteEndPoint);
    }

    public void OnData(IConnection connection, ReadOnlySpan<byte> line)
    {
        SessionImpl? session;
        lock (_sessionsLock)
        {
            if (!_connectionSessions.TryGetValue(connection.Id, out session))
                return;
        }

        if (session.State == SessionState.Disconnecting)
            return;

        if (!IrcLine.TryParse(line, out var parts))
        {
            _logger.LogDebug("Failed to parse IRC line from {Id}", session.Id);
            return;
        }

        // IRCv3 message-tags: client tag data MUST NOT exceed 4094 bytes
        if (parts.Tags.Length > IrcConstants.MaxTagsLength - 2)
        {
            session.SendNumericAsync(_server.Config.ServerName, IrcNumerics.ERR_INPUTTOOLONG,
                "Input line was too long").AsTask().Wait();
            return;
        }

        var commandStr = System.Text.Encoding.UTF8.GetString(parts.Command);
        var handler = _server.Commands.Resolve(commandStr);
        if (handler == null)
        {
            if (session.State >= SessionState.Registered)
            {
                session.SendNumericAsync(_server.Config.ServerName, IrcNumerics.ERR_UNKNOWNCOMMAND,
                    commandStr, "Unknown command").AsTask().Wait();
            }
            _logger.LogDebug("Unknown command {Command} from {Id}", commandStr, session.Id);
            return;
        }

        // Check minimum session state
        if (session.State < handler.MinimumState)
        {
            if (session.State < SessionState.Registered)
            {
                session.SendNumericAsync(_server.Config.ServerName, IrcNumerics.ERR_NOTREGISTERED,
                    "You have not registered").AsTask().Wait();
            }
            return;
        }

        // Check permission attributes
        var handlerType = handler.GetType();
        var serverPermAttr = handlerType.GetCustomAttributes(typeof(RequiresServerPermissionAttribute), false);
        if (serverPermAttr.Length > 0)
        {
            var attr = (RequiresServerPermissionAttribute)serverPermAttr[0];
            if (!Permissions.PermissionResolution.HasServerPermission(session.CachedServerPermissions, attr.Permission))
            {
                session.SendNumericAsync(_server.Config.ServerName, IrcNumerics.ERR_NOPRIVILEGES,
                    "Permission Denied").AsTask().Wait();
                return;
            }
        }

        // Flood protection
        var limiter = session.Info.FloodLimiter;
        if (limiter != null &&
            !Permissions.PermissionResolution.HasServerPermission(session.CachedServerPermissions, Permissions.ServerPermission.BypassThrottle))
        {
            var penaltyAttr = handlerType.GetCustomAttributes(typeof(FloodPenaltyAttribute), false);
            var cost = penaltyAttr.Length > 0 ? ((FloodPenaltyAttribute)penaltyAttr[0]).Cost : 1;

            if (cost > 0)
            {
                var floodResult = limiter.TryConsume(cost);
                if (floodResult == FloodResult.ExcessFlood)
                {
                    _logger.LogWarning("Excess flood from {Id}, disconnecting", session.Id);
                    _metrics.ExcessFloodDisconnect();
                    session.DisconnectAsync("Excess Flood").AsTask().Wait();
                    return;
                }
                if (floodResult == FloodResult.Throttled)
                {
                    _logger.LogDebug("Throttled {Command} from {Id}", commandStr, session.Id);
                    _metrics.CommandThrottled();
                    return;
                }
            }
        }

        var message = parts.ToMessage();
        session.Info.LastActivity = DateTimeOffset.UtcNow;
        _metrics.CommandDispatched();

        // Extract label tag for labeled-response support
        string? label = null;
        if (Capabilities.CapHelper.HasCap(session, "labeled-response") && message.Tags != null)
        {
            var parsed = message.ParsedTags;
            parsed.TryGetValue("label", out label);
        }

        // Enqueue command to per-session serialized processing queue
        session.CommandWriter.TryWrite(async () =>
        {
            var start = ServerMetrics.GetTimestamp();
            try
            {
                session.Info.CurrentLabel = label;
                session.Info.LabeledMessageCount = 0;

                await handler.HandleAsync(session, message);

                if (label != null && session.Info.LabeledMessageCount == 0)
                {
                    var tags = Capabilities.CapHelper.BuildTags(session);
                    if (tags != null)
                        await session.SendTaggedMessageAsync(tags, _server.Config.ServerName, IrcConstants.ACK, "*");
                    else
                        await session.SendMessageAsync(_server.Config.ServerName, IrcConstants.ACK, "*");
                }
            }
            catch (Exception ex)
            {
                _metrics.Error();
                _logger.LogError(ex, "Error handling {Command} from {Id}", commandStr, session.Id);
            }
            finally
            {
                session.Info.CurrentLabel = null;
                session.Info.LabeledMessageCount = 0;
                _metrics.RecordCommandDuration(commandStr, ServerMetrics.GetElapsedMs(start));
            }
        });
    }

    public void OnDisconnected(IConnection connection)
    {
        SessionImpl? session;
        lock (_sessionsLock)
        {
            if (!_connectionSessions.Remove(connection.Id, out session))
                return;
        }

        // Decrement per-IP counter
        var ip = session.Info.Hostname ?? "unknown";
        _ipConnectionCounts.AddOrUpdate(ip, 0, (_, count) => Math.Max(0, count - 1));

        var nick = session.Info.Nickname;
        if (nick != null)
        {
            // Record WHOWAS entry before removing from nick index
            _server.RecordWhowas(new Core.Sessions.WhowasEntry(
                nick,
                session.Info.Username ?? "~user",
                session.Info.Hostname ?? "unknown",
                session.Info.Realname ?? nick,
                DateTimeOffset.UtcNow));
            _server.UpdateNickIndex(nick, null, session);
        }
        _server.RemoveSession(session.Id);
        session.StopCommandProcessing();

        // Move broadcast off the transport thread to avoid blocking accept/receive
        _ = Task.Run(async () =>
        {
            try
            {
                // Broadcast QUIT to all users sharing channels (deduplicated)
                var notified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var quitReason = "Connection closed";
                foreach (var channelName in session.Info.Channels.ToList())
                {
                    if (_server.Channels.TryGetValue(channelName, out var channel))
                    {
                        foreach (var (memberNick, _) in channel.Members)
                        {
                            if (nick != null &&
                                string.Equals(memberNick, nick, StringComparison.OrdinalIgnoreCase))
                                continue;
                            if (notified.Add(memberNick))
                            {
                                var memberSession = _server.FindSessionByNick(memberNick);
                                if (memberSession != null)
                                    await CapHelper.SendWithTimestamp(memberSession, session.Info.Prefix, IrcConstants.QUIT, quitReason);
                            }
                        }

                        // Log QUIT event per channel for chathistory event-playback
                        _writeQueue?.TryWrite(new AddChatLog(new ChatLogEntity
                        {
                            ChannelName = channelName,
                            Sender = nick!,
                            Message = quitReason,
                            MessageType = IrcConstants.QUIT,
                            SentAt = DateTimeOffset.UtcNow,
                            MsgId = MsgIdGenerator.Generate(),
                        }));

                        channel.RemoveMember(nick!);
                        if (channel.Members.Count == 0)
                            _server.RemoveChannel(channelName);
                    }
                }

                _server.Events.Publish(new SessionDisconnectedEvent(session.Id, quitReason));

                // MONITOR: notify watchers that this nick is now offline
                if (nick != null)
                    await Handlers.Connection.MonitorHandler.NotifyOffline(_server, nick);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error broadcasting QUIT for {Id}", session.Id);
            }
        });

        _metrics.ConnectionClosed();
        _logger.LogInformation("Client disconnected: {Id}", session.Id);
    }
}
