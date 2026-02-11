namespace MeatSpeak.Server.Transport.Pools;

using System.Collections.Concurrent;
using System.Net.Sockets;

public sealed class SocketEventArgsPool
{
    private readonly ConcurrentQueue<SocketAsyncEventArgs> _pool = new();
    private readonly int _bufferSize;
    private int _totalCreated;

    public SocketEventArgsPool(int bufferSize = 4096, int preAllocate = 0)
    {
        _bufferSize = bufferSize;
        for (int i = 0; i < preAllocate; i++)
            _pool.Enqueue(CreateNew());
    }

    public SocketAsyncEventArgs Rent()
    {
        if (_pool.TryDequeue(out var args))
            return args;
        return CreateNew();
    }

    public void Return(SocketAsyncEventArgs args)
    {
        args.AcceptSocket = null;
        args.UserToken = null;
        args.SetBuffer(args.Buffer, 0, args.Buffer!.Length);
        _pool.Enqueue(args);
    }

    public int Available => _pool.Count;
    public int TotalCreated => _totalCreated;

    private SocketAsyncEventArgs CreateNew()
    {
        var args = new SocketAsyncEventArgs(unsafeSuppressExecutionContextFlow: true);
        var buffer = BufferPool.Rent(_bufferSize);
        args.SetBuffer(buffer, 0, buffer.Length);
        Interlocked.Increment(ref _totalCreated);
        return args;
    }
}
