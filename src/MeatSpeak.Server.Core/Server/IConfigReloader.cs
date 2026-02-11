namespace MeatSpeak.Server.Core.Server;

public interface IConfigReloader
{
    Task ReloadAsync(CancellationToken ct = default);
}
