using Xunit;
using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Permissions;

namespace MeatSpeak.Server.Data.Tests;

public class DatabaseSeederTests : IDisposable
{
    private readonly MeatSpeakDbContext _db;

    public DatabaseSeederTests()
    {
        var options = new DbContextOptionsBuilder<MeatSpeakDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new MeatSpeakDbContext(options);
        _db.Database.OpenConnection();
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task SeedAsync_CreatesTablesAndBuiltInRoles()
    {
        var seeder = new DatabaseSeeder(_db);
        await seeder.SeedAsync();

        var roles = await _db.Roles.ToListAsync();
        Assert.Equal(3, roles.Count);
        Assert.Contains(roles, r => r.Name == "@everyone" && r.Id == Guid.Empty);
        Assert.Contains(roles, r => r.Name == "Moderator");
        Assert.Contains(roles, r => r.Name == "Admin");
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        var seeder = new DatabaseSeeder(_db);
        await seeder.SeedAsync();
        await seeder.SeedAsync(); // second call should be a no-op

        var roles = await _db.Roles.ToListAsync();
        Assert.Equal(3, roles.Count);
    }

    [Fact]
    public async Task SeedAsync_CreatesAllTables()
    {
        var seeder = new DatabaseSeeder(_db);
        await seeder.SeedAsync();

        // Verify all DbSet tables exist by querying each
        Assert.Empty(await _db.ServerBans.ToListAsync());
        Assert.Empty(await _db.AuditLog.ToListAsync());
        Assert.Empty(await _db.UserRoles.ToListAsync());
        Assert.Empty(await _db.ChannelOverrides.ToListAsync());
        Assert.Empty(await _db.TopicHistory.ToListAsync());
        Assert.Empty(await _db.UserHistory.ToListAsync());
        Assert.Empty(await _db.ChatLogs.ToListAsync());
    }

    [Fact]
    public async Task SeedAsync_CreatesDefaultLobbyChannel()
    {
        var seeder = new DatabaseSeeder(_db);
        await seeder.SeedAsync();

        var channels = await _db.Channels.ToListAsync();
        Assert.Single(channels);
        Assert.Equal("#lobby", channels[0].Name);
        Assert.Equal("Welcome to MeatSpeak!", channels[0].Topic);
        Assert.Equal("server", channels[0].TopicSetBy);
        Assert.Equal("nt", channels[0].Modes);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_Channels()
    {
        var seeder = new DatabaseSeeder(_db);
        await seeder.SeedAsync();
        await seeder.SeedAsync();

        var channels = await _db.Channels.ToListAsync();
        Assert.Single(channels);
    }

    [Fact]
    public async Task SeedAsync_BuiltInRolesHaveCorrectPositions()
    {
        var seeder = new DatabaseSeeder(_db);
        await seeder.SeedAsync();

        var everyone = await _db.Roles.FindAsync(Guid.Empty);
        Assert.NotNull(everyone);
        Assert.Equal(0, everyone!.Position);

        var roles = await _db.Roles.OrderByDescending(r => r.Position).ToListAsync();
        var admin = roles.First(r => r.Name == "Admin");
        var mod = roles.First(r => r.Name == "Moderator");

        Assert.True(admin.Position > mod.Position);
        Assert.True(mod.Position > everyone.Position);
    }
}
