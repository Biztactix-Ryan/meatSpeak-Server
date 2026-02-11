using Xunit;
using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;

namespace MeatSpeak.Server.Data.Tests;

public class ChannelRepositoryTests : IDisposable
{
    private readonly MeatSpeakDbContext _db;
    private readonly ChannelRepository _repo;

    public ChannelRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeatSpeakDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new MeatSpeakDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _repo = new ChannelRepository(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task UpsertAsync_CreatesNewChannel()
    {
        var channel = new ChannelEntity
        {
            Id = Guid.NewGuid(),
            Name = "#test",
            Topic = "Test topic",
            TopicSetBy = "admin",
            TopicSetAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            Modes = "nt",
        };

        await _repo.UpsertAsync(channel);

        var fetched = await _repo.GetByNameAsync("#test");
        Assert.NotNull(fetched);
        Assert.Equal("#test", fetched!.Name);
        Assert.Equal("Test topic", fetched.Topic);
        Assert.Equal("nt", fetched.Modes);
    }

    [Fact]
    public async Task UpsertAsync_UpdatesExistingChannel()
    {
        await _repo.UpsertAsync(new ChannelEntity
        {
            Id = Guid.NewGuid(),
            Name = "#test",
            Topic = "Original",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await _repo.UpsertAsync(new ChannelEntity
        {
            Id = Guid.NewGuid(),
            Name = "#test",
            Topic = "Updated",
            TopicSetBy = "mod",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var all = await _repo.GetAllAsync();
        Assert.Single(all);
        Assert.Equal("Updated", all[0].Topic);
        Assert.Equal("mod", all[0].TopicSetBy);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOrderedByName()
    {
        await _repo.UpsertAsync(new ChannelEntity { Id = Guid.NewGuid(), Name = "#zebra", CreatedAt = DateTimeOffset.UtcNow });
        await _repo.UpsertAsync(new ChannelEntity { Id = Guid.NewGuid(), Name = "#alpha", CreatedAt = DateTimeOffset.UtcNow });
        await _repo.UpsertAsync(new ChannelEntity { Id = Guid.NewGuid(), Name = "#middle", CreatedAt = DateTimeOffset.UtcNow });

        var all = await _repo.GetAllAsync();
        Assert.Equal(3, all.Count);
        Assert.Equal("#alpha", all[0].Name);
        Assert.Equal("#middle", all[1].Name);
        Assert.Equal("#zebra", all[2].Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesChannel()
    {
        await _repo.UpsertAsync(new ChannelEntity { Id = Guid.NewGuid(), Name = "#temp", CreatedAt = DateTimeOffset.UtcNow });

        await _repo.DeleteAsync("#temp");

        var fetched = await _repo.GetByNameAsync("#temp");
        Assert.Null(fetched);
    }

    [Fact]
    public async Task DeleteAsync_NoOpForNonexistent()
    {
        await _repo.DeleteAsync("#nonexistent"); // should not throw
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsNullForMissing()
    {
        var result = await _repo.GetByNameAsync("#missing");
        Assert.Null(result);
    }
}
