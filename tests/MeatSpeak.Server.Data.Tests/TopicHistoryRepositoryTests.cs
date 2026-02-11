using Xunit;
using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;

namespace MeatSpeak.Server.Data.Tests;

public class TopicHistoryRepositoryTests : IDisposable
{
    private readonly MeatSpeakDbContext _db;
    private readonly TopicHistoryRepository _repo;

    public TopicHistoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeatSpeakDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new MeatSpeakDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _repo = new TopicHistoryRepository(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task AddAsync_And_GetByChannelAsync()
    {
        await _repo.AddAsync(new TopicHistoryEntity
        {
            ChannelName = "#test",
            Topic = "First topic",
            SetBy = "admin",
            SetAt = DateTimeOffset.UtcNow,
        });

        var results = await _repo.GetByChannelAsync("#test");
        Assert.Single(results);
        Assert.Equal("First topic", results[0].Topic);
        Assert.Equal("admin", results[0].SetBy);
    }

    [Fact]
    public async Task GetByChannelAsync_ReturnsNewestFirst()
    {
        var baseTime = DateTimeOffset.UtcNow;

        await _repo.AddAsync(new TopicHistoryEntity
        {
            ChannelName = "#test",
            Topic = "Older",
            SetBy = "user1",
            SetAt = baseTime.AddMinutes(-10),
        });
        await _repo.AddAsync(new TopicHistoryEntity
        {
            ChannelName = "#test",
            Topic = "Newer",
            SetBy = "user2",
            SetAt = baseTime,
        });

        var results = await _repo.GetByChannelAsync("#test");
        Assert.Equal(2, results.Count);
        Assert.Equal("Newer", results[0].Topic);
        Assert.Equal("Older", results[1].Topic);
    }

    [Fact]
    public async Task GetByChannelAsync_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            await _repo.AddAsync(new TopicHistoryEntity
            {
                ChannelName = "#test",
                Topic = $"Topic {i}",
                SetBy = "admin",
                SetAt = DateTimeOffset.UtcNow.AddMinutes(i),
            });
        }

        var results = await _repo.GetByChannelAsync("#test", limit: 3);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task GetByChannelAsync_FiltersbyChannel()
    {
        await _repo.AddAsync(new TopicHistoryEntity { ChannelName = "#a", Topic = "A", SetBy = "x", SetAt = DateTimeOffset.UtcNow });
        await _repo.AddAsync(new TopicHistoryEntity { ChannelName = "#b", Topic = "B", SetBy = "x", SetAt = DateTimeOffset.UtcNow });

        var resultsA = await _repo.GetByChannelAsync("#a");
        Assert.Single(resultsA);
        Assert.Equal("A", resultsA[0].Topic);
    }
}
