namespace MeatSpeak.Server.Core.Events;

public interface IEventBus
{
    void Publish<T>(T evt) where T : ServerEvent;
    IDisposable Subscribe<T>(Action<T> handler) where T : ServerEvent;
}
