namespace MeatSpeak.Server.Handlers.Channels;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Channels;
using MeatSpeak.Server.Core.Commands;
using MeatSpeak.Server.Core.Events;
using MeatSpeak.Server.Core.Modes;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class ModeHandler : ICommandHandler
{
    private readonly IServer _server;
    public string Command => IrcConstants.MODE;
    public SessionState MinimumState => SessionState.Registered;

    public ModeHandler(IServer server) => _server = server;

    public async ValueTask HandleAsync(ISession session, IrcMessage message, CancellationToken ct = default)
    {
        if (message.Parameters.Count < 1)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NEEDMOREPARAMS,
                IrcConstants.MODE, "Not enough parameters");
            return;
        }

        var target = message.GetParam(0)!;

        if (target.StartsWith('#'))
            await HandleChannelMode(session, message, target);
        else
            await HandleUserMode(session, message, target);
    }

    private async ValueTask HandleUserMode(ISession session, IrcMessage message, string target)
    {
        // Users can only query/change their own mode
        if (!string.Equals(target, session.Info.Nickname, StringComparison.OrdinalIgnoreCase))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_USERSDONTMATCH,
                "Cannot change mode for other users");
            return;
        }

        if (message.Parameters.Count < 2)
        {
            // RPL_UMODEIS - query user modes
            var modeString = session.Info.UserModes.Count > 0
                ? "+" + new string(session.Info.UserModes.OrderBy(c => c).ToArray())
                : "+";
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_UMODEIS, modeString);
            return;
        }

        var modeStr = message.GetParam(1)!;
        var adding = true;
        var applied = new List<char>();
        var appliedDir = new List<bool>();

        foreach (var c in modeStr)
        {
            if (c == '+') { adding = true; continue; }
            if (c == '-') { adding = false; continue; }

            // Users can only set +i and +w on themselves; +o can only be removed
            switch (c)
            {
                case 'i':
                case 'w':
                    if (adding)
                        session.Info.UserModes.Add(c);
                    else
                        session.Info.UserModes.Remove(c);
                    applied.Add(c);
                    appliedDir.Add(adding);
                    break;
                case 'o':
                    if (!adding)
                    {
                        session.Info.UserModes.Remove(c);
                        applied.Add(c);
                        appliedDir.Add(false);
                    }
                    break;
            }
        }

        if (applied.Count > 0)
        {
            var result = BuildModeString(applied, appliedDir);
            await session.SendMessageAsync(session.Info.Prefix, IrcConstants.MODE, target, result);
        }
    }

    private async ValueTask HandleChannelMode(ISession session, IrcMessage message, string channelName)
    {
        if (!_server.Channels.TryGetValue(channelName, out var channel))
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_NOSUCHCHANNEL,
                channelName, "No such channel");
            return;
        }

        if (message.Parameters.Count < 2)
        {
            // Query channel modes
            var modeString = channel.Modes.Count > 0
                ? "+" + new string(channel.Modes.OrderBy(c => c).ToArray())
                : "+";

            var modeParams = new List<string> { channelName, modeString };
            if (channel.Key != null)
                modeParams.Add(channel.Key);
            if (channel.UserLimit.HasValue)
                modeParams.Add(channel.UserLimit.Value.ToString());

            await session.SendNumericAsync(_server.Config.ServerName, Numerics.RPL_CHANNELMODEIS,
                modeParams.ToArray());
            return;
        }

        // Check chanop for setting modes
        var membership = channel.GetMember(session.Info.Nickname!);
        if (membership == null || !membership.IsOperator)
        {
            await session.SendNumericAsync(_server.Config.ServerName, Numerics.ERR_CHANOPRIVSNEEDED,
                channelName, "You're not channel operator");
            return;
        }

        var modeStr = message.GetParam(1)!;
        var adding = true;
        int paramIdx = 2;
        var appliedModes = new List<(bool adding, char mode, string? param)>();

        foreach (var c in modeStr)
        {
            if (c == '+') { adding = true; continue; }
            if (c == '-') { adding = false; continue; }

            switch (c)
            {
                case 'o':
                case 'v':
                    await HandleMemberStatus(channel, c, adding, message, paramIdx, appliedModes);
                    paramIdx++;
                    break;
                case 'b':
                    await HandleBanMode(session, channel, adding, message, paramIdx, appliedModes);
                    if (adding || paramIdx < message.Parameters.Count)
                        paramIdx++;
                    break;
                case 'e':
                    await HandleExceptMode(session, channel, adding, message, paramIdx, appliedModes);
                    if (adding || paramIdx < message.Parameters.Count)
                        paramIdx++;
                    break;
                case 'k':
                    HandleKeyMode(channel, adding, message, paramIdx, appliedModes);
                    paramIdx++;
                    break;
                case 'l':
                    HandleLimitMode(channel, adding, message, ref paramIdx, appliedModes);
                    break;
                default:
                    // Type D flag modes (n, t, i, m, s, V, S, E)
                    var modeDef = _server.Modes.ResolveChannelMode(c);
                    if (modeDef != null && modeDef.Type == ModeType.D)
                    {
                        if (adding)
                            channel.Modes.Add(c);
                        else
                            channel.Modes.Remove(c);
                        appliedModes.Add((adding, c, null));
                    }
                    break;
            }
        }

        if (appliedModes.Count > 0)
        {
            var (modeResult, paramResult) = BuildChannelModeString(appliedModes);
            var broadcastParams = new List<string> { channelName, modeResult };
            if (!string.IsNullOrEmpty(paramResult))
                broadcastParams.Add(paramResult);

            foreach (var (memberNick, _) in channel.Members)
            {
                var memberSession = _server.FindSessionByNick(memberNick);
                if (memberSession != null)
                    await memberSession.SendMessageAsync(session.Info.Prefix, IrcConstants.MODE, broadcastParams.ToArray());
            }

            _server.Events.Publish(new ModeChangedEvent(channelName,
                modeResult + (string.IsNullOrEmpty(paramResult) ? "" : " " + paramResult),
                session.Info.Nickname!));
        }
    }

    private async ValueTask HandleBanMode(ISession session, IChannel channel, bool adding, IrcMessage message,
        int paramIdx, List<(bool adding, char mode, string? param)> appliedModes)
    {
        if (!adding && paramIdx >= message.Parameters.Count)
        {
            // No param on -b with no mask? Ignore.
            return;
        }

        if (adding && paramIdx >= message.Parameters.Count)
        {
            // List bans
            foreach (var ban in channel.Bans)
            {
                await session.SendNumericAsync(_server.Config.ServerName, 367,
                    channel.Name, ban.Mask, ban.SetBy, ban.SetAt.ToUnixTimeSeconds().ToString());
            }
            await session.SendNumericAsync(_server.Config.ServerName, 368,
                channel.Name, "End of channel ban list");
            return;
        }

        var mask = message.GetParam(paramIdx)!;
        if (adding)
        {
            channel.AddBan(new BanEntry(mask, session.Info.Nickname!, DateTimeOffset.UtcNow));
            appliedModes.Add((true, 'b', mask));
        }
        else
        {
            if (channel.RemoveBan(mask))
                appliedModes.Add((false, 'b', mask));
        }
    }

    private async ValueTask HandleExceptMode(ISession session, IChannel channel, bool adding, IrcMessage message,
        int paramIdx, List<(bool adding, char mode, string? param)> appliedModes)
    {
        if (!adding && paramIdx >= message.Parameters.Count)
            return;

        if (adding && paramIdx >= message.Parameters.Count)
        {
            // List exceptions
            foreach (var except in channel.Excepts)
            {
                await session.SendNumericAsync(_server.Config.ServerName, 348,
                    channel.Name, except.Mask, except.SetBy, except.SetAt.ToUnixTimeSeconds().ToString());
            }
            await session.SendNumericAsync(_server.Config.ServerName, 349,
                channel.Name, "End of channel exception list");
            return;
        }

        var mask = message.GetParam(paramIdx)!;
        if (adding)
        {
            channel.AddExcept(new BanEntry(mask, session.Info.Nickname!, DateTimeOffset.UtcNow));
            appliedModes.Add((true, 'e', mask));
        }
        else
        {
            if (channel.RemoveExcept(mask))
                appliedModes.Add((false, 'e', mask));
        }
    }

    private static ValueTask HandleMemberStatus(IChannel channel, char mode, bool adding, IrcMessage message,
        int paramIdx, List<(bool adding, char mode, string? param)> appliedModes)
    {
        if (paramIdx >= message.Parameters.Count)
            return ValueTask.CompletedTask;

        var nick = message.GetParam(paramIdx)!;
        var memberShip = channel.GetMember(nick);
        if (memberShip == null)
            return ValueTask.CompletedTask;

        if (mode == 'o')
        {
            memberShip.IsOperator = adding;
            appliedModes.Add((adding, 'o', nick));
        }
        else if (mode == 'v')
        {
            memberShip.HasVoice = adding;
            appliedModes.Add((adding, 'v', nick));
        }

        return ValueTask.CompletedTask;
    }

    private static void HandleKeyMode(IChannel channel, bool adding, IrcMessage message,
        int paramIdx, List<(bool adding, char mode, string? param)> appliedModes)
    {
        if (paramIdx >= message.Parameters.Count)
            return;

        var param = message.GetParam(paramIdx)!;
        if (adding)
        {
            channel.Key = param;
            channel.Modes.Add('k');
            appliedModes.Add((true, 'k', param));
        }
        else
        {
            channel.Key = null;
            channel.Modes.Remove('k');
            appliedModes.Add((false, 'k', "*"));
        }
    }

    private static void HandleLimitMode(IChannel channel, bool adding, IrcMessage message,
        ref int paramIdx, List<(bool adding, char mode, string? param)> appliedModes)
    {
        if (adding)
        {
            if (paramIdx >= message.Parameters.Count)
                return;
            var param = message.GetParam(paramIdx)!;
            if (int.TryParse(param, out var limit) && limit > 0)
            {
                channel.UserLimit = limit;
                channel.Modes.Add('l');
                appliedModes.Add((true, 'l', param));
            }
            paramIdx++;
        }
        else
        {
            channel.UserLimit = null;
            channel.Modes.Remove('l');
            appliedModes.Add((false, 'l', null));
        }
    }

    private static string BuildModeString(List<char> applied, List<bool> dirs)
    {
        var result = new System.Text.StringBuilder();
        bool? lastDir = null;
        for (int i = 0; i < applied.Count; i++)
        {
            if (lastDir != dirs[i])
            {
                result.Append(dirs[i] ? '+' : '-');
                lastDir = dirs[i];
            }
            result.Append(applied[i]);
        }
        return result.ToString();
    }

    private static (string modes, string parms) BuildChannelModeString(
        List<(bool adding, char mode, string? param)> applied)
    {
        var modes = new System.Text.StringBuilder();
        var parms = new List<string>();
        bool? lastDir = null;

        foreach (var (adding, mode, param) in applied)
        {
            if (lastDir != adding)
            {
                modes.Append(adding ? '+' : '-');
                lastDir = adding;
            }
            modes.Append(mode);
            if (param != null)
                parms.Add(param);
        }

        return (modes.ToString(), string.Join(" ", parms));
    }
}
