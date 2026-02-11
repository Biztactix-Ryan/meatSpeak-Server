using Xunit;
using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;

namespace MeatSpeak.Server.Data.Tests;

public class ChatLogRepositoryTests : IDisposable
{
    private readonly MeatSpeakDbContext _db;
    private readonly ChatLogRepository _repo;

    public ChatLogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeatSpeakDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new MeatSpeakDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _repo = new ChatLogRepository(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task AddAsync_And_GetByChannelAsync()
    {
        await _repo.AddAsync(new ChatLogEntity
        {
            ChannelName = "#test",
            Sender = "user1",
            Message = "Hello world",
            MessageType = "PRIVMSG",
            SentAt = DateTimeOffset.UtcNow,
        });

        var results = await _repo.GetByChannelAsync("#test");
        Assert.Single(results);
        Assert.Equal("Hello world", results[0].Message);
        Assert.Equal("user1", results[0].Sender);
        Assert.Equal("PRIVMSG", results[0].MessageType);
    }

    [Fact]
    public async Task GetByChannelAsync_ReturnsNewestFirst()
    {
        var baseTime = DateTimeOffset.UtcNow;
        await _repo.AddAsync(new ChatLogEntity
        {
            ChannelName = "#test",
            Sender = "user1",
            Message = "Older message",
            MessageType = "PRIVMSG",
            SentAt = baseTime.AddMinutes(-10),
        });
        await _repo.AddAsync(new ChatLogEntity
        {
            ChannelName = "#test",
            Sender = "user2",
            Message = "Newer message",
            MessageType = "PRIVMSG",
            SentAt = baseTime,
        });

        var results = await _repo.GetByChannelAsync("#test");
        Assert.Equal(2, results.Count);
        Assert.Equal("Newer message", results[0].Message);
    }

    [Fact]
    public async Task GetByChannelAsync_RespectsLimitAndOffset()
    {
        for (int i = 0; i < 10; i++)
        {
            await _repo.AddAsync(new ChatLogEntity
            {
                ChannelName = "#test",
                Sender = "user",
                Message = $"Message {i}",
                MessageType = "PRIVMSG",
                SentAt = DateTimeOffset.UtcNow.AddMinutes(i),
            });
        }

        var results = await _repo.GetByChannelAsync("#test", limit: 3, offset: 2);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task GetByChannelAsync_FiltersByChannel()
    {
        await _repo.AddAsync(new ChatLogEntity { ChannelName = "#a", Sender = "x", Message = "A", MessageType = "PRIVMSG", SentAt = DateTimeOffset.UtcNow });
        await _repo.AddAsync(new ChatLogEntity { ChannelName = "#b", Sender = "x", Message = "B", MessageType = "PRIVMSG", SentAt = DateTimeOffset.UtcNow });

        var results = await _repo.GetByChannelAsync("#a");
        Assert.Single(results);
        Assert.Equal("A", results[0].Message);
    }

    [Fact]
    public async Task GetPrivateMessagesAsync_ReturnsBothDirections()
    {
        await _repo.AddAsync(new ChatLogEntity
        {
            ChannelName = null,
            Target = "bob",
            Sender = "alice",
            Message = "Hi Bob",
            MessageType = "PRIVMSG",
            SentAt = DateTimeOffset.UtcNow.AddMinutes(-1),
        });
        await _repo.AddAsync(new ChatLogEntity
        {
            ChannelName = null,
            Target = "alice",
            Sender = "bob",
            Message = "Hi Alice",
            MessageType = "PRIVMSG",
            SentAt = DateTimeOffset.UtcNow,
        });

        var results = await _repo.GetPrivateMessagesAsync("alice", "bob");
        Assert.Equal(2, results.Count);

        // Also works in reverse order
        var results2 = await _repo.GetPrivateMessagesAsync("bob", "alice");
        Assert.Equal(2, results2.Count);
    }

    [Fact]
    public async Task GetPrivateMessagesAsync_ExcludesChannelMessages()
    {
        await _repo.AddAsync(new ChatLogEntity
        {
            ChannelName = "#test",
            Sender = "alice",
            Message = "Channel msg",
            MessageType = "PRIVMSG",
            SentAt = DateTimeOffset.UtcNow,
        });
        await _repo.AddAsync(new ChatLogEntity
        {
            ChannelName = null,
            Target = "bob",
            Sender = "alice",
            Message = "PM",
            MessageType = "PRIVMSG",
            SentAt = DateTimeOffset.UtcNow,
        });

        var results = await _repo.GetPrivateMessagesAsync("alice", "bob");
        Assert.Single(results);
        Assert.Equal("PM", results[0].Message);
    }
}
