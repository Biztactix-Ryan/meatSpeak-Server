namespace MeatSpeak.Server.Tls;

public interface IAcmeChallengeHandler
{
    Task PrepareAsync(string domain, string token, string keyAuth);
    Task CleanupAsync(string domain, string token);
}
