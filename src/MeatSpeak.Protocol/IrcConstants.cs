namespace MeatSpeak.Protocol;

public static class IrcConstants
{
    /// <summary>
    /// Per RFC 1459, the maximum line length is 512 bytes INCLUDING CR/LF.
    /// This means the actual message content can be at most 510 bytes.
    /// </summary>
    public const int MaxLineLength = 512;
    
    /// <summary>
    /// Maximum message content length (excluding CR/LF terminator).
    /// Per RFC 1459: MaxLineLength (512) - CR (1) - LF (1) = 510 bytes.
    /// </summary>
    public const int MaxMessageLength = 510;
    
    /// <summary>
    /// Maximum line length with IRCv3 message tags.
    /// Tags can add up to 4096 bytes plus the base message limit.
    /// </summary>
    public const int MaxLineLengthWithTags = 4096 + 512;
    
    /// <summary>
    /// Maximum tags content length per IRCv3 specification.
    /// </summary>
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
}
