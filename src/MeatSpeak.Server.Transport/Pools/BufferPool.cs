namespace MeatSpeak.Server.Transport.Pools;

using System.Buffers;

public static class BufferPool
{
    private static readonly ArrayPool<byte> Pool = ArrayPool<byte>.Shared;

    public static byte[] Rent(int minimumLength) => Pool.Rent(minimumLength);

    public static void Return(byte[] buffer, bool clearArray = false) => Pool.Return(buffer, clearArray);
}
