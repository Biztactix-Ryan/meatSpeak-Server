namespace MeatSpeak.Server.Core.Sessions;

using System.Diagnostics;

public enum FloodResult
{
    Allowed,
    Throttled,
    ExcessFlood,
}

public sealed class FloodLimiter
{
    private readonly int _burstLimit;
    private readonly double _tokenIntervalTicks;
    private readonly int _excessFloodThreshold;
    private readonly object _lock = new();

    private double _tokens;
    private long _lastTimestamp;
    private int _debt;

    public FloodLimiter(int burstLimit, double tokenIntervalSeconds, int excessFloodThreshold)
    {
        _burstLimit = burstLimit;
        _tokenIntervalTicks = tokenIntervalSeconds * Stopwatch.Frequency;
        _excessFloodThreshold = excessFloodThreshold;
        _tokens = burstLimit;
        _lastTimestamp = Stopwatch.GetTimestamp();
    }

    public FloodResult TryConsume(int cost = 1)
    {
        lock (_lock)
        {
            Refill();

            if (_tokens >= cost)
            {
                _tokens -= cost;
                _debt = 0;
                return FloodResult.Allowed;
            }

            _debt += cost;

            if (_debt >= _excessFloodThreshold)
                return FloodResult.ExcessFlood;

            return FloodResult.Throttled;
        }
    }

    private void Refill()
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = now - _lastTimestamp;

        if (elapsed > 0 && _tokenIntervalTicks > 0)
        {
            var newTokens = elapsed / _tokenIntervalTicks;
            _tokens = Math.Min(_burstLimit, _tokens + newTokens);
            _lastTimestamp = now;

            // Reduce debt as tokens regenerate
            if (_tokens > 0 && _debt > 0)
            {
                var debtReduction = (int)_tokens;
                _debt = Math.Max(0, _debt - debtReduction);
            }
        }
    }
}
