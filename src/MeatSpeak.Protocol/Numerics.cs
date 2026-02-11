namespace MeatSpeak.Protocol;

public static class Numerics
{
    // Welcome
    public const int RPL_WELCOME = 1;
    public const int RPL_YOURHOST = 2;
    public const int RPL_CREATED = 3;
    public const int RPL_MYINFO = 4;
    public const int RPL_ISUPPORT = 5;

    // Luser
    public const int RPL_LUSERCLIENT = 251;
    public const int RPL_LUSEROP = 252;
    public const int RPL_LUSERUNKNOWN = 253;
    public const int RPL_LUSERCHANNELS = 254;
    public const int RPL_LUSERME = 255;

    // Whois / Who
    public const int RPL_AWAY = 301;
    public const int RPL_WHOISUSER = 311;
    public const int RPL_WHOISSERVER = 312;
    public const int RPL_WHOISOPERATOR = 313;
    public const int RPL_ENDOFWHO = 315;
    public const int RPL_ENDOFWHOIS = 318;
    public const int RPL_WHOISCHANNELS = 319;

    // Channel
    public const int RPL_LIST = 322;
    public const int RPL_LISTEND = 323;
    public const int RPL_CHANNELMODEIS = 324;
    public const int RPL_NOTOPIC = 331;
    public const int RPL_TOPIC = 332;
    public const int RPL_TOPICWHOTIME = 333;
    public const int RPL_INVITING = 341;
    public const int RPL_VERSION = 351;
    public const int RPL_WHOREPLY = 352;
    public const int RPL_NAMREPLY = 353;
    public const int RPL_ENDOFNAMES = 366;
    public const int RPL_BANLIST = 367;
    public const int RPL_ENDOFBANLIST = 368;

    // MOTD
    public const int RPL_MOTD = 372;
    public const int RPL_MOTDSTART = 375;
    public const int RPL_ENDOFMOTD = 376;

    // Oper
    public const int RPL_YOUREOPER = 381;

    // Voice
    public const int RPL_VOICESESSION = 900;
    public const int RPL_VOICESTATE = 901;
    public const int RPL_VOICELIST = 902;
    public const int RPL_ENDOFVOICELIST = 903;
    public const int RPL_VOICEKEY = 904;
    public const int RPL_VOICEERROR = 905;

    // Errors
    public const int ERR_NOSUCHNICK = 401;
    public const int ERR_NOSUCHCHANNEL = 403;
    public const int ERR_CANNOTSENDTOCHAN = 404;
    public const int ERR_TOOMANYCHANNELS = 405;
    public const int ERR_NONICKNAMEGIVEN = 431;
    public const int ERR_ERRONEUSNICKNAME = 432;
    public const int ERR_NICKNAMEINUSE = 433;
    public const int ERR_USERNOTINCHANNEL = 441;
    public const int ERR_NOTONCHANNEL = 442;
    public const int ERR_USERONCHANNEL = 443;
    public const int ERR_NOTREGISTERED = 451;
    public const int ERR_NEEDMOREPARAMS = 461;
    public const int ERR_ALREADYREGISTRED = 462;
    public const int ERR_PASSWDMISMATCH = 464;
    public const int ERR_CHANNELISFULL = 471;
    public const int ERR_INVITEONLYCHAN = 473;
    public const int ERR_BANNEDFROMCHAN = 474;
    public const int ERR_BADCHANNELKEY = 475;
    public const int ERR_NOPRIVILEGES = 481;
    public const int ERR_CHANOPRIVSNEEDED = 482;
    public const int ERR_NOOPERHOST = 491;

    public static string Format(int numeric) => numeric.ToString("D3");
}
