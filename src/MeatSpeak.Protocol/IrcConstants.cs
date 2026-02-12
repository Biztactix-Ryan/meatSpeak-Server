namespace MeatSpeak.Protocol;

public static class IrcConstants
{
    public const int MaxLineLength = 512;
    public const int MaxLineLengthWithTags = 4096 + 512;
    public const int MaxTagsLength = 4096;
    public const byte CR = (byte)'\r';
    public const byte LF = (byte)'\n';
    public const byte Space = (byte)' ';
    public const byte Colon = (byte)':';
    public const byte At = (byte)'@';
    public const byte Semicolon = (byte)';';
    public const byte Equals = (byte)'=';
    public const byte Backslash = (byte)'\\';

    // Common command strings
    public const string PASS = "PASS";
    public const string NICK = "NICK";
    public const string USER = "USER";
    public const string QUIT = "QUIT";
    public const string JOIN = "JOIN";
    public const string PART = "PART";
    public const string MODE = "MODE";
    public const string TOPIC = "TOPIC";
    public const string NAMES = "NAMES";
    public const string LIST = "LIST";
    public const string INVITE = "INVITE";
    public const string KICK = "KICK";
    public const string PRIVMSG = "PRIVMSG";
    public const string NOTICE = "NOTICE";
    public const string PING = "PING";
    public const string PONG = "PONG";
    public const string CAP = "CAP";
    public const string WHO = "WHO";
    public const string WHOIS = "WHOIS";
    public const string MOTD = "MOTD";
    public const string LUSERS = "LUSERS";
    public const string VERSION = "VERSION";
    public const string OPER = "OPER";
    public const string KILL = "KILL";
    public const string VOICE = "VOICE";
    public const string AUTHENTICATE = "AUTHENTICATE";
    public const string REHASH = "REHASH";
    public const string AWAY = "AWAY";
    public const string WHOWAS = "WHOWAS";
    public const string BATCH = "BATCH";
    public const string TAGMSG = "TAGMSG";
    public const string REDACT = "REDACT";
    public const string CHATHISTORY = "CHATHISTORY";
    public const string MONITOR = "MONITOR";
    public const string ACK = "ACK";
}
