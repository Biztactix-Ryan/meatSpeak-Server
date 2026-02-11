namespace MeatSpeak.Server.Tls;

using System.Collections.Concurrent;

public sealed class Http01ChallengeHandler : IAcmeChallengeHandler
{
    private readonly ConcurrentDictionary<string, string> _challenges = new();

    public string? GetResponse(string token)
    {
        _challenges.TryGetValue(token, out var response);
        return response;
    }

    public Task PrepareAsync(string domain, string token, string keyAuth)
    {
        _challenges[token] = keyAuth;
        return Task.CompletedTask;
    }

    public Task CleanupAsync(string domain, string token)
    {
        _challenges.TryRemove(token, out _);
        return Task.CompletedTask;
    }
}
