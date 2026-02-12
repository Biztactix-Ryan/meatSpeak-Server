using System.Diagnostics;
using MeatSpeak.Protocol;

namespace MeatSpeak.Benchmark;

public sealed class SimulatedUser
{
    private readonly int _userId;
    private readonly BenchmarkOptions _opts;
    private readonly Random _rng;
    private readonly ActionGenerator _actions;
    private readonly UserMetrics _metrics;
    private IIrcTransport? _transport;
    private volatile bool _running;
    private bool _registered;

    public UserMetrics Metrics => _metrics;

    public SimulatedUser(int userId, BenchmarkOptions opts)
    {
        _userId = userId;
        _opts = opts;
        _rng = new Random(opts.Seed + userId);
        _actions = new ActionGenerator(_rng, opts.Channels);
        _metrics = new UserMetrics { UserId = userId };
    }

    public async Task RunAsync(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            _transport = _opts.Transport == "ws"
                ? new WsTransport(_opts.WsPath)
                : new TcpTransport();

            await _transport.ConnectAsync(_opts.Host, _opts.Port, ct);
            _metrics.ConnectTimeMs = sw.ElapsedMilliseconds;

            _running = true;
            using var readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var readTask = ReadLoopAsync(readCts.Token);

            // Register
            var nick = $"user{_userId}";
            await SendAsync($"NICK {nick}", ct);
            await SendAsync($"USER bench{_userId} 0 * :Benchmark User {_userId}", ct);

            // Wait for registration (001) with timeout
            var regDeadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            regDeadline.CancelAfter(TimeSpan.FromSeconds(_opts.RegTimeout));
            try
            {
                while (!_registered && !regDeadline.Token.IsCancellationRequested)
                    await Task.Delay(10, regDeadline.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _metrics.Errors++;
                if (!_opts.Quiet)
                    Console.WriteLine($"[user{_userId}] Registration timeout");
            }

            // Perform actions
            if (_registered)
            {
                for (int i = 0; i < _opts.Actions && !ct.IsCancellationRequested; i++)
                {
                    var actionSw = Stopwatch.StartNew();
                    try
                    {
                        var line = _actions.Execute(_userId);
                        if (line != null)
                        {
                            await SendAsync(line, ct);
                            _metrics.ActionsCompleted++;
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _metrics.Errors++;
                        if (!_opts.Quiet)
                            Console.WriteLine($"[user{_userId}] Action error: {ex.Message}");
                    }
                    _metrics.ActionTimesMs.Add(actionSw.ElapsedMilliseconds);

                    if (_opts.Delay > 0)
                        await Task.Delay(_opts.Delay, ct);
                }
            }

            // Quit
            try
            {
                await SendAsync("QUIT :Benchmark complete", ct);
                await Task.Delay(100, ct);
            }
            catch { /* best effort */ }

            _running = false;
            readCts.Cancel();
            try { await readTask; } catch { /* expected */ }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _metrics.Errors++;
            if (!_opts.Quiet)
                Console.WriteLine($"[user{_userId}] Fatal: {ex.Message}");
        }
        finally
        {
            _running = false;
            if (_transport != null)
            {
                _metrics.BytesSent = _transport.BytesSent;
                _metrics.BytesReceived = _transport.BytesReceived;
                await _transport.DisposeAsync();
            }
        }
    }

    private async Task SendAsync(string line, CancellationToken ct)
    {
        if (_transport == null) return;
        await _transport.SendLineAsync(line, ct);
        if (!_opts.Quiet)
            Console.WriteLine($"[user{_userId}] >> {line}");
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_transport == null) return;
        try
        {
            while (_running && !ct.IsCancellationRequested)
            {
                var line = await _transport.ReadLineAsync(ct);
                if (line == null) break;

                if (!_opts.Quiet)
                    Console.WriteLine($"[user{_userId}] << {line}");

                HandleServerLine(line);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (_running && !ct.IsCancellationRequested)
            {
                _metrics.Errors++;
                if (!_opts.Quiet)
                    Console.WriteLine($"[user{_userId}] Read error: {ex.Message}");
            }
        }
    }

    private void HandleServerLine(string line)
    {
        // Quick parse: look for PING and 001
        if (line.StartsWith("PING "))
        {
            var token = line[5..];
            // Fire-and-forget PONG
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_transport != null)
                        await _transport.SendLineAsync($"PONG {token}", CancellationToken.None);
                }
                catch { /* best effort */ }
            });
            return;
        }

        // Check for prefix-based PING: ":server PING ..."
        var spaceIdx = line.IndexOf(' ');
        if (spaceIdx > 0)
        {
            var afterPrefix = line[(spaceIdx + 1)..];
            if (afterPrefix.StartsWith("PING "))
            {
                var token = afterPrefix[5..];
                _ = Task.Run(async () =>
                {
                    try
                    {
                        if (_transport != null)
                            await _transport.SendLineAsync($"PONG {token}", CancellationToken.None);
                    }
                    catch { /* best effort */ }
                });
                return;
            }

            // Check for 001 (RPL_WELCOME)
            var nextSpace = afterPrefix.IndexOf(' ');
            if (nextSpace > 0)
            {
                var command = afterPrefix[..nextSpace];
                if (command == "001")
                    _registered = true;
            }
        }
    }
}
