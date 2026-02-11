namespace MeatSpeak.Benchmark;

public sealed class ActionGenerator
{
    private readonly Random _rng;
    private readonly int _channelCount;
    private readonly HashSet<string> _joinedChannels = new();
    private int _nickVersion;

    private static readonly string[] Words =
    [
        "meat", "speak", "bench", "load", "test", "irc", "chat", "hello",
        "world", "server", "client", "fast", "cool", "great", "nice",
        "alpha", "beta", "gamma", "delta", "epsilon", "zeta", "eta",
        "theta", "iota", "kappa", "lambda", "omega", "sigma", "tau",
    ];

    private static readonly (int weight, string name)[] ActionWeights =
    [
        (15, "JoinChannel"),
        (10, "PartChannel"),
        (30, "SendMessage"),
        (10, "ChangeNick"),
        (8, "SetTopic"),
        (5, "SetMode"),
        (5, "ListChannels"),
        (5, "WhoChannel"),
        (5, "SendNotice"),
        (7, "NamesChannel"),
    ];

    private static readonly int TotalWeight = ActionWeights.Sum(a => a.weight);

    public ActionGenerator(Random rng, int channelCount)
    {
        _rng = rng;
        _channelCount = channelCount;
    }

    public string? Execute(int userId)
    {
        var roll = _rng.Next(TotalWeight);
        var cumulative = 0;
        string action = ActionWeights[^1].name;

        foreach (var (weight, name) in ActionWeights)
        {
            cumulative += weight;
            if (roll < cumulative)
            {
                action = name;
                break;
            }
        }

        return action switch
        {
            "JoinChannel" => DoJoinChannel(),
            "PartChannel" => DoPartChannel(),
            "SendMessage" => DoSendMessage(),
            "ChangeNick" => DoChangeNick(userId),
            "SetTopic" => DoSetTopic(),
            "SetMode" => DoSetMode(),
            "ListChannels" => "LIST",
            "WhoChannel" => DoWhoChannel(),
            "SendNotice" => DoSendNotice(),
            "NamesChannel" => DoNamesChannel(),
            _ => null,
        };
    }

    private string DoJoinChannel()
    {
        var channel = $"#bench-{_rng.Next(_channelCount)}";
        _joinedChannels.Add(channel);
        return $"JOIN {channel}";
    }

    private string? DoPartChannel()
    {
        if (_joinedChannels.Count == 0) return DoJoinChannel();
        var channel = PickRandomJoined();
        _joinedChannels.Remove(channel);
        return $"PART {channel}";
    }

    private string? DoSendMessage()
    {
        if (_joinedChannels.Count == 0) return DoJoinChannel();
        var channel = PickRandomJoined();
        return $"PRIVMSG {channel} :{RandomText()}";
    }

    private string DoChangeNick(int userId)
    {
        _nickVersion++;
        return $"NICK user{userId}_v{_nickVersion}";
    }

    private string? DoSetTopic()
    {
        if (_joinedChannels.Count == 0) return DoJoinChannel();
        var channel = PickRandomJoined();
        return $"TOPIC {channel} :{RandomText()}";
    }

    private string? DoSetMode()
    {
        if (_joinedChannels.Count == 0) return DoJoinChannel();
        var channel = PickRandomJoined();
        var mode = _rng.Next(2) == 0 ? "+m" : "-m";
        return $"MODE {channel} {mode}";
    }

    private string? DoWhoChannel()
    {
        if (_joinedChannels.Count == 0) return DoJoinChannel();
        return $"WHO {PickRandomJoined()}";
    }

    private string? DoSendNotice()
    {
        if (_joinedChannels.Count == 0) return DoJoinChannel();
        var channel = PickRandomJoined();
        return $"NOTICE {channel} :{RandomText()}";
    }

    private string? DoNamesChannel()
    {
        if (_joinedChannels.Count == 0) return DoJoinChannel();
        return $"NAMES {PickRandomJoined()}";
    }

    private string PickRandomJoined()
    {
        var idx = _rng.Next(_joinedChannels.Count);
        var i = 0;
        foreach (var ch in _joinedChannels)
        {
            if (i == idx) return ch;
            i++;
        }
        return _joinedChannels.First();
    }

    private string RandomText()
    {
        var count = _rng.Next(2, 8);
        var parts = new string[count];
        for (int i = 0; i < count; i++)
            parts[i] = Words[_rng.Next(Words.Length)];
        return string.Join(' ', parts);
    }
}
