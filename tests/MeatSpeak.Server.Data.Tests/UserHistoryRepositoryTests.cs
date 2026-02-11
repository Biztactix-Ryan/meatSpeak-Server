using Xunit;
using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;

namespace MeatSpeak.Server.Data.Tests;

public class UserHistoryRepositoryTests : IDisposable
{
    private readonly MeatSpeakDbContext _db;
    private readonly UserHistoryRepository _repo;

    public UserHistoryRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeatSpeakDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new MeatSpeakDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _repo = new UserHistoryRepository(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task AddAsync_And_GetByNicknameAsync()
    {
        await _repo.AddAsync(new UserHistoryEntity
        {
            Nickname = "testuser",
            Username = "test",
            Hostname = "localhost",
            ConnectedAt = DateTimeOffset.UtcNow,
        });

        var results = await _repo.GetByNicknameAsync("testuser");
        Assert.Single(results);
        Assert.Equal("testuser", results[0].Nickname);
        Assert.Equal("test", results[0].Username);
    }

    [Fact]
    public async Task UpdateDisconnectAsync_SetsDisconnectFields()
    {
        var entry = new UserHistoryEntity
        {
            Nickname = "testuser",
            Username = "test",
            Hostname = "localhost",
            ConnectedAt = DateTimeOffset.UtcNow,
        };
        await _repo.AddAsync(entry);

        var disconnectTime = DateTimeOffset.UtcNow;
        await _repo.UpdateDisconnectAsync(entry.Id, disconnectTime, "Client quit");

        var results = await _repo.GetByNicknameAsync("testuser");
        Assert.Single(results);
        Assert.NotNull(results[0].DisconnectedAt);
        Assert.Equal("Client quit", results[0].QuitReason);
    }

    [Fact]
    public async Task GetByAccountAsync_FiltersCorrectly()
    {
        await _repo.AddAsync(new UserHistoryEntity
        {
            Nickname = "user1",
            Account = "account1",
            ConnectedAt = DateTimeOffset.UtcNow,
        });
        await _repo.AddAsync(new UserHistoryEntity
        {
            Nickname = "user2",
            Account = "account2",
            ConnectedAt = DateTimeOffset.UtcNow,
        });

        var results = await _repo.GetByAccountAsync("account1");
        Assert.Single(results);
        Assert.Equal("user1", results[0].Nickname);
    }

    [Fact]
    public async Task GetByNicknameAsync_RespectsLimit()
    {
        for (int i = 0; i < 10; i++)
        {
            await _repo.AddAsync(new UserHistoryEntity
            {
                Nickname = "testuser",
                ConnectedAt = DateTimeOffset.UtcNow.AddMinutes(i),
            });
        }

        var results = await _repo.GetByNicknameAsync("testuser", limit: 3);
        Assert.Equal(3, results.Count);
    }

    [Fact]
    public async Task GetByNicknameAsync_ReturnsNewestFirst()
    {
        var baseTime = DateTimeOffset.UtcNow;
        await _repo.AddAsync(new UserHistoryEntity
        {
            Nickname = "testuser",
            Username = "old",
            ConnectedAt = baseTime.AddMinutes(-10),
        });
        await _repo.AddAsync(new UserHistoryEntity
        {
            Nickname = "testuser",
            Username = "new",
            ConnectedAt = baseTime,
        });

        var results = await _repo.GetByNicknameAsync("testuser");
        Assert.Equal("new", results[0].Username);
    }
}
