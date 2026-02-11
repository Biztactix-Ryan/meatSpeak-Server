namespace MeatSpeak.Server.Core.Modes;

public sealed class ModeRegistry
{
    private readonly Dictionary<char, ModeDefinition> _channelModes = new();
    private readonly Dictionary<char, ModeDefinition> _userModes = new();

    public void RegisterChannelMode(ModeDefinition mode)
    {
        _channelModes[mode.Char] = mode;
    }

    public void RegisterUserMode(ModeDefinition mode)
    {
        _userModes[mode.Char] = mode;
    }

    public ModeDefinition? ResolveChannelMode(char c) =>
        _channelModes.TryGetValue(c, out var m) ? m : null;

    public ModeDefinition? ResolveUserMode(char c) =>
        _userModes.TryGetValue(c, out var m) ? m : null;

    public IReadOnlyDictionary<char, ModeDefinition> ChannelModes => _channelModes;
    public IReadOnlyDictionary<char, ModeDefinition> UserModes => _userModes;

    /// <summary>
    /// Generates ISUPPORT CHANMODES=A,B,C,D string
    /// </summary>
    public string GetChanModesIsupport()
    {
        var a = new List<char>();
        var b = new List<char>();
        var c = new List<char>();
        var d = new List<char>();

        foreach (var (ch, def) in _channelModes)
        {
            switch (def.Type)
            {
                case ModeType.A: a.Add(ch); break;
                case ModeType.B: b.Add(ch); break;
                case ModeType.C: c.Add(ch); break;
                case ModeType.D: d.Add(ch); break;
            }
        }

        a.Sort(); b.Sort(); c.Sort(); d.Sort();
        return $"CHANMODES={new string(a.ToArray())},{new string(b.ToArray())},{new string(c.ToArray())},{new string(d.ToArray())}";
    }

    public void RegisterStandardModes()
    {
        // Standard IRC channel modes
        RegisterChannelMode(new ModeDefinition('b', ModeType.A, "ban"));
        RegisterChannelMode(new ModeDefinition('e', ModeType.A, "ban-exception"));
        RegisterChannelMode(new ModeDefinition('k', ModeType.B, "key"));
        RegisterChannelMode(new ModeDefinition('l', ModeType.C, "limit"));
        RegisterChannelMode(new ModeDefinition('i', ModeType.D, "invite-only"));
        RegisterChannelMode(new ModeDefinition('m', ModeType.D, "moderated"));
        RegisterChannelMode(new ModeDefinition('n', ModeType.D, "no-external-messages"));
        RegisterChannelMode(new ModeDefinition('t', ModeType.D, "topic-protected"));
        RegisterChannelMode(new ModeDefinition('s', ModeType.D, "secret"));

        // MeatSpeak custom channel modes
        RegisterChannelMode(new ModeDefinition('V', ModeType.D, "voice-enabled", true));
        RegisterChannelMode(new ModeDefinition('S', ModeType.D, "spatial-audio", true));
        RegisterChannelMode(new ModeDefinition('E', ModeType.D, "e2e-voice", true));

        // Standard user modes
        RegisterUserMode(new ModeDefinition('i', ModeType.D, "invisible"));
        RegisterUserMode(new ModeDefinition('o', ModeType.D, "operator"));
        RegisterUserMode(new ModeDefinition('w', ModeType.D, "wallops"));
    }
}
