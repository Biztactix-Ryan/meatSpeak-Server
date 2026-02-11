namespace MeatSpeak.Server;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Diagnostics;
using MeatSpeak.Server.State;
using MeatSpeak.Server.Transport;
using Microsoft.Extensions.Logging;
using IrcNumerics = MeatSpeak.Protocol.Numerics;

public sealed class IrcConnectionHandler : IConnectionHandler
{
    private readonly IServer _server;
    private readonly ILogger<IrcConnectionHandler> _logger;
    private readonly ServerMetrics _metrics;
    private readonly Dictionary<string, SessionImpl> _connectionSessions = new();
    private readonly object _sessionsLock = new();

    public IrcConnectionHandler(IServer server, ILogger<IrcConnectionHandler> logger, ServerMetrics metrics)
    {
        _server = server;
        _logger = logger;
        _metrics = metrics;
    }

    public void OnConnected(IConnection connection)
    {
        var session = new SessionImpl(connection, _server.Config.ServerName);
        lock (_sessionsLock)
            _connectionSessions[connection.Id] = session;
        _server.AddSession(session);
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

        if (!IrcLine.TryParse(line, out var parts))
        {
            _logger.LogDebug("Failed to parse IRC line from {Id}", session.Id);
            return;
        }

        var commandStr = System.Text.Encoding.UTF8.GetString(parts.Command);
        var handler = _server.Commands.Resolve(commandStr);
        if (handler == null)
        {
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

        var message = parts.ToMessage();
        session.Info.LastActivity = DateTimeOffset.UtcNow;
        _metrics.CommandDispatched();

        // Fire and forget the handler (we're on the transport callback thread)
        _ = Task.Run(async () =>
        {
            var start = ServerMetrics.GetTimestamp();
            try
            {
                await handler.HandleAsync(session, message);
            }
            catch (Exception ex)
            {
                _metrics.Error();
                _logger.LogError(ex, "Error handling {Command} from {Id}", commandStr, session.Id);
            }
            finally
            {
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

        var nick = session.Info.Nickname;
        if (nick != null)
            _server.UpdateNickIndex(nick, null, session);
        _server.RemoveSession(session.Id);

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
                                    await memberSession.SendMessageAsync(session.Info.Prefix, IrcConstants.QUIT, quitReason);
                            }
                        }

                        channel.RemoveMember(nick!);
                        if (channel.Members.Count == 0)
                            _server.RemoveChannel(channelName);
                    }
                }

                _server.Events.Publish(new SessionDisconnectedEvent(session.Id, quitReason));
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
