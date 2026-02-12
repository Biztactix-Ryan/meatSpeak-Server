namespace MeatSpeak.Server.Numerics;

using MeatSpeak.Protocol;
using MeatSpeak.Server.Core.Sessions;
using MeatSpeak.Server.Core.Server;

public sealed class NumericSender
{
    private readonly IServer _server;

    public NumericSender(IServer server) => _server = server;

    public async ValueTask SendWelcomeAsync(ISession session)
    {
        var nick = session.Info.Nickname!;
        var config = _server.Config;

        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_WELCOME,
            $"Welcome to the {config.NetworkName} Network, {session.Info.Prefix}");
        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_YOURHOST,
            $"Your host is {config.ServerName}, running version {config.Version}");
        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_CREATED,
            $"This server was created {_server.StartedAt:R}");
        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_MYINFO,
            $"{config.ServerName} {config.Version} iow bklmnstVSE");
    }

    public async ValueTask SendIsupportAsync(ISession session)
    {
        var config = _server.Config;
        var chanModes = _server.Modes.GetChanModesIsupport();
        var tokens = new[]
        {
            $"NETWORK={config.NetworkName}",
            chanModes,
            "PREFIX=(ov)@+",
            "CHANTYPES=#",
            "CASEMAPPING=ascii",
            "NICKLEN=32",
            "CHANNELLEN=64",
            "TOPICLEN=390",
            "STATUSMSG=@+",
            $"CHANLIMIT=#:{config.MaxChannelsPerUser}",
            "MODES=3",
            "EXCEPTS=e",
            "INVEX=I",
            "MSGREFTYPES=timestamp,msgid",
            "CHATHISTORY=100",
            "MONITOR=100",
            "are supported by this server",
        };
        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_ISUPPORT, tokens);
    }

    public async ValueTask SendMotdAsync(ISession session)
    {
        var config = _server.Config;
        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_MOTDSTART,
            $"- {config.ServerName} Message of the Day -");
        foreach (var line in config.Motd)
            await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_MOTD, $"- {line}");
        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_ENDOFMOTD,
            "End of /MOTD command.");
    }

    public async ValueTask SendLusersAsync(ISession session)
    {
        var config = _server.Config;
        var userCount = _server.ConnectionCount;
        var channelCount = _server.ChannelCount;

        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_LUSERCLIENT,
            $"There are {userCount} users and 0 invisible on 1 servers");
        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_LUSEROP,
            "0", "IRC Operators online");
        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_LUSERUNKNOWN,
            "0", "unknown connection(s)");
        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_LUSERCHANNELS,
            channelCount.ToString(), "channels formed");
        await session.SendNumericAsync(config.ServerName, Protocol.Numerics.RPL_LUSERME,
            $"I have {userCount} clients and 0 servers");
    }
}
