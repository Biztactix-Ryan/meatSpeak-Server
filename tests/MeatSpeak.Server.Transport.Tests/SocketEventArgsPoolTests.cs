using System.Net.Sockets;
using MeatSpeak.Server.Transport.Pools;
using Xunit;

namespace MeatSpeak.Server.Transport.Tests;

public class SocketEventArgsPoolTests
{
    [Fact]
    public void Rent_ReturnsSocketAsyncEventArgsWithBuffer()
    {
        var pool = new SocketEventArgsPool(bufferSize: 4096);

        var args = pool.Rent();

        Assert.NotNull(args);
        Assert.NotNull(args.Buffer);
        Assert.True(args.Buffer.Length >= 4096);
        Assert.Equal(0, args.Offset);
        Assert.Equal(args.Buffer.Length, args.Count);

        args.Dispose();
    }

    [Fact]
    public void ReturnAndReRent_ReusesObject()
    {
        var pool = new SocketEventArgsPool(bufferSize: 4096);

        var first = pool.Rent();
        pool.Return(first);
        var second = pool.Rent();

        Assert.Same(first, second);

        second.Dispose();
    }

    [Fact]
    public void Return_ResetsAcceptSocketAndUserToken()
    {
        var pool = new SocketEventArgsPool(bufferSize: 4096);

        var args = pool.Rent();
        args.UserToken = "some token";
        pool.Return(args);
        var reused = pool.Rent();

        Assert.Null(reused.AcceptSocket);
        Assert.Null(reused.UserToken);

        reused.Dispose();
    }

    [Fact]
    public void PreAllocate_CreatesExpectedCount()
    {
        var pool = new SocketEventArgsPool(bufferSize: 2048, preAllocate: 5);

        Assert.Equal(5, pool.Available);
        Assert.Equal(5, pool.TotalCreated);
    }

    [Fact]
    public void PreAllocate_Zero_StartsEmpty()
    {
        var pool = new SocketEventArgsPool(bufferSize: 4096, preAllocate: 0);

        Assert.Equal(0, pool.Available);
        Assert.Equal(0, pool.TotalCreated);
    }

    [Fact]
    public void Rent_FromPreAllocated_DecreasesAvailable()
    {
        var pool = new SocketEventArgsPool(bufferSize: 4096, preAllocate: 3);

        Assert.Equal(3, pool.Available);

        var first = pool.Rent();
        Assert.Equal(2, pool.Available);

        var second = pool.Rent();
        Assert.Equal(1, pool.Available);

        // TotalCreated should not increase from renting pre-allocated items
        Assert.Equal(3, pool.TotalCreated);

        first.Dispose();
        second.Dispose();
    }

    [Fact]
    public void Rent_WhenPoolEmpty_CreatesNew()
    {
        var pool = new SocketEventArgsPool(bufferSize: 4096, preAllocate: 0);

        var args = pool.Rent();

        Assert.NotNull(args);
        Assert.Equal(1, pool.TotalCreated);

        args.Dispose();
    }

    [Fact]
    public void Return_ResetsBufferOffsetAndCount()
    {
        var pool = new SocketEventArgsPool(bufferSize: 4096);

        var args = pool.Rent();
        var originalBuffer = args.Buffer;
        var originalLength = args.Buffer!.Length;
        // Simulate partial use by changing the buffer window
        args.SetBuffer(args.Buffer, 10, 100);
        pool.Return(args);
        var reused = pool.Rent();

        Assert.Equal(0, reused.Offset);
        Assert.Equal(originalLength, reused.Count);

        reused.Dispose();
    }
}
