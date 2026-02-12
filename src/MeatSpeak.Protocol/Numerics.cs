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

    // Away
    public const int RPL_AWAY = 301;
    public const int RPL_UNAWAY = 305;
    public const int RPL_NOWAWAY = 306;

    // User mode
    public const int RPL_UMODEIS = 221;

    // Whois / Who
    public const int RPL_WHOISUSER = 311;
    public const int RPL_WHOISSERVER = 312;
    public const int RPL_WHOISOPERATOR = 313;
    public const int RPL_WHOWASUSER = 314;
    public const int RPL_ENDOFWHO = 315;
    public const int RPL_WHOISIDLE = 317;
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
    public const int RPL_ENDOFWHOWAS = 369;

    // MOTD
    public const int RPL_MOTD = 372;
    public const int RPL_MOTDSTART = 375;
    public const int RPL_ENDOFMOTD = 376;

    // Oper
    public const int RPL_YOUREOPER = 381;

    // SASL (IRCv3)
    public const int RPL_LOGGEDIN = 900;
    public const int RPL_LOGGEDOUT = 901;
    public const int ERR_NICKLOCKED = 902;
    public const int RPL_SASLSUCCESS = 903;
    public const int ERR_SASLFAIL = 904;
    public const int ERR_SASLTOOLONG = 905;
    public const int ERR_SASLABORTED = 906;
    public const int ERR_SASLALREADY = 907;
    public const int RPL_SASLMECHS = 908;

    // Voice (custom)
    public const int RPL_VOICESESSION = 750;
    public const int RPL_VOICESTATE = 751;
    public const int RPL_VOICELIST = 752;
    public const int RPL_ENDOFVOICELIST = 753;
    public const int RPL_VOICEKEY = 754;
    public const int RPL_VOICEERROR = 755;

    // Monitor
    public const int RPL_MONONLINE = 730;
    public const int RPL_MONOFFLINE = 731;
    public const int RPL_MONLIST = 732;
    public const int RPL_ENDOFMONLIST = 733;
    public const int ERR_MONLISTFULL = 734;

    // Errors
    public const int ERR_NOSUCHNICK = 401;
    public const int ERR_NOSUCHSERVER = 402;
    public const int ERR_NOSUCHCHANNEL = 403;
    public const int ERR_CANNOTSENDTOCHAN = 404;
    public const int ERR_TOOMANYCHANNELS = 405;
    public const int ERR_WASNOSUCHNICK = 406;
    public const int ERR_NOORIGIN = 409;
    public const int ERR_NORECIPIENT = 411;
    public const int ERR_NOTEXTTOSEND = 412;
    public const int ERR_INPUTTOOLONG = 417;
    public const int ERR_UNKNOWNCOMMAND = 421;
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
    public const int ERR_YOUREBANNEDCREEP = 465;
    public const int ERR_CHANNELISFULL = 471;
    public const int ERR_INVITEONLYCHAN = 473;
    public const int ERR_BANNEDFROMCHAN = 474;
    public const int ERR_BADCHANNELKEY = 475;
    public const int ERR_NOPRIVILEGES = 481;
    public const int ERR_CHANOPRIVSNEEDED = 482;
    public const int ERR_NOOPERHOST = 491;
    public const int ERR_UMODEUNKNOWNFLAG = 501;
    public const int ERR_USERSDONTMATCH = 502;

    public static string Format(int numeric) => numeric.ToString("D3");
}
