namespace MeatSpeak.Benchmark;

public sealed record BenchmarkOptions
{
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 6667;
    public string Transport { get; init; } = "tcp";
    public string WsPath { get; init; } = "/irc";
    public int Users { get; init; } = 100;
    public int Concurrency { get; init; } = 50;
    public int Seed { get; init; } = 0;
    public int Channels { get; init; } = 10;
    public int Actions { get; init; } = 20;
    public int Delay { get; init; } = 50;
    public bool Quiet { get; init; } = false;
    public int MaxErrors { get; init; } = -1;
    public int RegTimeout { get; init; } = 10;
    public string? OutputPath { get; init; }

    public static BenchmarkOptions Parse(string[] args)
    {
        var opts = new BenchmarkOptions();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host" when i + 1 < args.Length:
                    opts = opts with { Host = args[++i] };
                    break;
                case "--port" when i + 1 < args.Length:
                    opts = opts with { Port = int.Parse(args[++i]) };
                    break;
                case "--transport" when i + 1 < args.Length:
                    opts = opts with { Transport = args[++i].ToLowerInvariant() };
                    break;
                case "--ws-path" when i + 1 < args.Length:
                    opts = opts with { WsPath = args[++i] };
                    break;
                case "--users" when i + 1 < args.Length:
                    opts = opts with { Users = int.Parse(args[++i]) };
                    break;
                case "--concurrency" when i + 1 < args.Length:
                    opts = opts with { Concurrency = int.Parse(args[++i]) };
                    break;
                case "--seed" when i + 1 < args.Length:
                    opts = opts with { Seed = int.Parse(args[++i]) };
                    break;
                case "--channels" when i + 1 < args.Length:
                    opts = opts with { Channels = int.Parse(args[++i]) };
                    break;
                case "--actions" when i + 1 < args.Length:
                    opts = opts with { Actions = int.Parse(args[++i]) };
                    break;
                case "--delay" when i + 1 < args.Length:
                    opts = opts with { Delay = int.Parse(args[++i]) };
                    break;
                case "--quiet":
                    opts = opts with { Quiet = true };
                    break;
                case "--max-errors" when i + 1 < args.Length:
                    opts = opts with { MaxErrors = int.Parse(args[++i]) };
                    break;
                case "--reg-timeout" when i + 1 < args.Length:
                    opts = opts with { RegTimeout = int.Parse(args[++i]) };
                    break;
                case "--output" when i + 1 < args.Length:
                    opts = opts with { OutputPath = args[++i] };
                    break;
                case "--help" or "-h":
                    PrintUsage();
                    Environment.Exit(0);
                    break;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    PrintUsage();
                    Environment.Exit(1);
                    break;
            }
        }
        return opts;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Usage: MeatSpeak.Benchmark [options]

            Options:
              --host <host>          Server hostname (default: localhost)
              --port <port>          Server port (default: 6667)
              --transport <tcp|ws>   Transport type (default: tcp)
              --ws-path <path>       WebSocket path (default: /irc)
              --users <n>            Total simulated users (default: 100)
              --concurrency <n>      Max simultaneous connections (default: 50)
              --seed <n>             RNG seed, 0 = random (default: 0)
              --channels <n>         Channel pool size (default: 10)
              --actions <n>          Actions per user (default: 20)
              --delay <ms>           Delay between actions (default: 50)
              --quiet                Suppress per-user logging
              --max-errors <n>       Exit with code 1 if errors exceed n (default: -1 = disabled)
              --reg-timeout <s>      Registration timeout in seconds (default: 10)
              --output <path>        Write JSON results to file
              --help, -h             Show this help
            """);
    }
}
