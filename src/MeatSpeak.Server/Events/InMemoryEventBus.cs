namespace MeatSpeak.Server.Events;

using System.Collections.Concurrent;
using MeatSpeak.Server.Core.Events;

public sealed class InMemoryEventBus : IEventBus
{
    private readonly ConcurrentDictionary<Type, List<Delegate>> _subscriptions = new();

    public void Publish<T>(T evt) where T : ServerEvent
    {
        if (_subscriptions.TryGetValue(typeof(T), out var handlers))
        {
            lock (handlers)
            {
                foreach (var handler in handlers)
                    ((Action<T>)handler)(evt);
            }
        }
    }

    public IDisposable Subscribe<T>(Action<T> handler) where T : ServerEvent
    {
        var handlers = _subscriptions.GetOrAdd(typeof(T), _ => new List<Delegate>());
        lock (handlers)
        {
            handlers.Add(handler);
        }
        return new Subscription(() =>
        {
            lock (handlers)
            {
                handlers.Remove(handler);
            }
        });
    }

    private sealed class Subscription : IDisposable
    {
        private readonly Action _onDispose;
        public Subscription(Action onDispose) => _onDispose = onDispose;
        public void Dispose() => _onDispose();
    }
}
