namespace MeatSpeak.Server.Handlers.Connection;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Capabilities;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

[FloodPenalty(1)]
public sealed class MonitorHandler : ICommandHandler
{
    private readonly IServer _server;
    private const int MaxMonitorEntries = 100;
    public string Command => IrcConstants.MONITOR;
    public SessionState MinimumState => SessionState.Registered;

    public MonitorHandler(IServer server)
    {
        _server = server;
    }

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 1)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.MONITOR, "Not enough parameters");
            return;
        }

        var subCommand = message.GetParam(0)!;
        switch (subCommand)
        {
            case "+":
                await HandleAdd(session, message);
                break;
            case "-":
                await HandleRemove(session, message);
                break;
            case "C":
            case "c":
                HandleClear(session);
                break;
            case "L":
            case "l":
                await HandleList(session);
                break;
            case "S":
            case "s":
                await HandleStatus(session);
                break;
        }
    }

    private async ValueTask HandleAdd(ISession session, IrcMessage message)
    {
        var targets = message.GetParam(1);
        if (string.IsNullOrEmpty(targets)) return;

        var nicks = targets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var online = new List<string>();
        var offline = new List<string>();

        foreach (var nick in nicks)
        {
            if (session.Info.MonitorList.Count >= MaxMonitorEntries)
            {
                await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_MONLISTFULL,
                    MaxMonitorEntries.ToString(), nick, "Monitor list is full");
                break;
            }

            session.Info.MonitorList.Add(nick);

            var target = _server.FindSessionByNick(nick);
            if (target != null && target.State >= SessionState.Registered)
                online.Add(target.Info.Prefix);
            else
                offline.Add(nick);
        }

        if (online.Count > 0)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_MONONLINE,
                string.Join(',', online));
        }
        if (offline.Count > 0)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_MONOFFLINE,
                string.Join(',', offline));
        }
    }

    private ValueTask HandleRemove(ISession session, IrcMessage message)
    {
        var targets = message.GetParam(1);
        if (string.IsNullOrEmpty(targets)) return ValueTask.CompletedTask;

        var nicks = targets.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var nick in nicks)
            session.Info.MonitorList.Remove(nick);

        return ValueTask.CompletedTask;
    }

    private void HandleClear(ISession session)
    {
        session.Info.MonitorList.Clear();
    }

    private async ValueTask HandleList(ISession session)
    {
        if (session.Info.MonitorList.Count > 0)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_MONLIST,
                string.Join(',', session.Info.MonitorList));
        }
        await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_ENDOFMONLIST,
            "End of MONITOR list");
    }

    private async ValueTask HandleStatus(ISession session)
    {
        var online = new List<string>();
        var offline = new List<string>();

        foreach (var nick in session.Info.MonitorList)
        {
            var target = _server.FindSessionByNick(nick);
            if (target != null && target.State >= SessionState.Registered)
                online.Add(target.Info.Prefix);
            else
                offline.Add(nick);
        }

        if (online.Count > 0)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_MONONLINE,
                string.Join(',', online));
        }
        if (offline.Count > 0)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_MONOFFLINE,
                string.Join(',', offline));
        }
    }

    /// <summary>
    /// Called when a user signs on (registration complete) or changes nick.
    /// Notifies all sessions monitoring the given nick.
    /// </summary>
    public static async ValueTask NotifyOnline(IServer server, ISession onlineSession)
    {
        var nick = onlineSession.Info.Nickname;
        if (nick == null) return;

        foreach (var (_, session) in server.Sessions)
        {
            if (session.Id == onlineSession.Id) continue;
            if (session.State < SessionState.Registered) continue;
            if (session.Info.MonitorList.Contains(nick))
            {
                await CapHelper.SendWithTimestamp(session, server.Config.ServerName,
                    Numerics.Format(Numerics.RPL_MONONLINE),
                    session.Info.Nickname ?? "*", onlineSession.Info.Prefix);
            }
        }
    }

    /// <summary>
    /// Called when a user signs off (QUIT/disconnect) or changes nick away from a monitored name.
    /// Notifies all sessions monitoring the given nick.
    /// </summary>
    public static async ValueTask NotifyOffline(IServer server, string nick)
    {
        foreach (var (_, session) in server.Sessions)
        {
            if (session.State < SessionState.Registered) continue;
            if (session.Info.MonitorList.Contains(nick))
            {
                await CapHelper.SendWithTimestamp(session, server.Config.ServerName,
                    Numerics.Format(Numerics.RPL_MONOFFLINE),
                    session.Info.Nickname ?? "*", nick);
            }
        }
    }
}
