using Xunit;
using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;

namespace MeatSpeak.Server.Data.Tests;

public class BanRepositoryTests : IDisposable
{
    private readonly MeatSpeakDbContext _db;
    private readonly BanRepository _repo;

    public BanRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeatSpeakDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new MeatSpeakDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _repo = new BanRepository(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task AddAsync_And_GetAllActiveAsync()
    {
        var ban = new ServerBanEntity
        {
            Id = Guid.NewGuid(),
            Mask = "*!*@bad.host",
            Reason = "spam",
            SetBy = "admin",
            SetAt = DateTimeOffset.UtcNow,
        };

        await _repo.AddAsync(ban);

        var all = await _repo.GetAllActiveAsync();
        Assert.Single(all);
        Assert.Equal("*!*@bad.host", all[0].Mask);
    }

    [Fact]
    public async Task GetAllActiveAsync_ExcludesExpired()
    {
        var expired = new ServerBanEntity
        {
            Id = Guid.NewGuid(),
            Mask = "*!*@old.host",
            SetBy = "admin",
            SetAt = DateTimeOffset.UtcNow.AddHours(-2),
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(-1),
        };
        var active = new ServerBanEntity
        {
            Id = Guid.NewGuid(),
            Mask = "*!*@bad.host",
            SetBy = "admin",
            SetAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
        };

        await _repo.AddAsync(expired);
        await _repo.AddAsync(active);

        var all = await _repo.GetAllActiveAsync();
        Assert.Single(all);
        Assert.Equal("*!*@bad.host", all[0].Mask);
    }

    [Fact]
    public async Task IsBannedAsync_ReturnsTrueForActiveBan()
    {
        await _repo.AddAsync(new ServerBanEntity
        {
            Id = Guid.NewGuid(),
            Mask = "*!*@evil.net",
            SetBy = "admin",
            SetAt = DateTimeOffset.UtcNow,
        });

        Assert.True(await _repo.IsBannedAsync("*!*@evil.net"));
        Assert.False(await _repo.IsBannedAsync("*!*@good.net"));
    }

    [Fact]
    public async Task RemoveAsync_DeletesBan()
    {
        var id = Guid.NewGuid();
        await _repo.AddAsync(new ServerBanEntity
        {
            Id = id,
            Mask = "*!*@temp.host",
            SetBy = "admin",
            SetAt = DateTimeOffset.UtcNow,
        });

        await _repo.RemoveAsync(id);

        var all = await _repo.GetAllActiveAsync();
        Assert.Empty(all);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsCorrectBan()
    {
        var id = Guid.NewGuid();
        await _repo.AddAsync(new ServerBanEntity
        {
            Id = id,
            Mask = "*!*@find.me",
            SetBy = "admin",
            SetAt = DateTimeOffset.UtcNow,
        });

        var ban = await _repo.GetByIdAsync(id);
        Assert.NotNull(ban);
        Assert.Equal("*!*@find.me", ban!.Mask);
    }
}

public class AuditLogRepositoryTests : IDisposable
{
    private readonly MeatSpeakDbContext _db;
    private readonly AuditLogRepository _repo;

    public AuditLogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeatSpeakDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new MeatSpeakDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _repo = new AuditLogRepository(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task AddAsync_And_QueryAsync()
    {
        await _repo.AddAsync(new AuditLogEntity
        {
            Action = "ban.add",
            Actor = "admin",
            Target = "*!*@bad",
            Details = "spam",
        });

        var results = await _repo.QueryAsync();
        Assert.Single(results);
        Assert.Equal("ban.add", results[0].Action);
    }

    [Fact]
    public async Task QueryAsync_FiltersByActor()
    {
        await _repo.AddAsync(new AuditLogEntity { Action = "ban.add", Actor = "admin1" });
        await _repo.AddAsync(new AuditLogEntity { Action = "ban.add", Actor = "admin2" });

        var results = await _repo.QueryAsync(actor: "admin1");
        Assert.Single(results);
        Assert.Equal("admin1", results[0].Actor);
    }

    [Fact]
    public async Task QueryAsync_FiltersByAction()
    {
        await _repo.AddAsync(new AuditLogEntity { Action = "ban.add", Actor = "admin" });
        await _repo.AddAsync(new AuditLogEntity { Action = "ban.remove", Actor = "admin" });

        var results = await _repo.QueryAsync(action: "ban.remove");
        Assert.Single(results);
        Assert.Equal("ban.remove", results[0].Action);
    }

    [Fact]
    public async Task QueryAsync_RespectsLimitAndOffset()
    {
        for (int i = 0; i < 10; i++)
            await _repo.AddAsync(new AuditLogEntity { Action = $"action.{i}", Actor = "admin" });

        var results = await _repo.QueryAsync(limit: 3, offset: 2);
        Assert.Equal(3, results.Count);
    }
}

public class RoleRepositoryTests : IDisposable
{
    private readonly MeatSpeakDbContext _db;
    private readonly RoleRepository _repo;

    public RoleRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<MeatSpeakDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;
        _db = new MeatSpeakDbContext(options);
        _db.Database.OpenConnection();
        _db.Database.EnsureCreated();
        _repo = new RoleRepository(_db);
    }

    public void Dispose()
    {
        _db.Database.CloseConnection();
        _db.Dispose();
    }

    [Fact]
    public async Task AddAsync_And_GetByIdAsync()
    {
        var role = new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = "TestRole",
            Position = 10,
            ServerPermissions = 0,
            DefaultChannelPermissions = 0,
        };
        await _repo.AddAsync(role);

        var fetched = await _repo.GetByIdAsync(role.Id);
        Assert.NotNull(fetched);
        Assert.Equal("TestRole", fetched!.Name);
    }

    [Fact]
    public async Task GetByNameAsync_FindsRole()
    {
        await _repo.AddAsync(new RoleEntity
        {
            Id = Guid.NewGuid(),
            Name = "Admin",
            Position = 100,
        });

        var role = await _repo.GetByNameAsync("Admin");
        Assert.NotNull(role);
        Assert.Equal("Admin", role!.Name);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByPosition()
    {
        await _repo.AddAsync(new RoleEntity { Id = Guid.NewGuid(), Name = "High", Position = 100 });
        await _repo.AddAsync(new RoleEntity { Id = Guid.NewGuid(), Name = "Low", Position = 1 });
        await _repo.AddAsync(new RoleEntity { Id = Guid.NewGuid(), Name = "Mid", Position = 50 });

        var all = await _repo.GetAllAsync();
        Assert.Equal("Low", all[0].Name);
        Assert.Equal("Mid", all[1].Name);
        Assert.Equal("High", all[2].Name);
    }

    [Fact]
    public async Task DeleteAsync_RemovesRole()
    {
        var id = Guid.NewGuid();
        await _repo.AddAsync(new RoleEntity { Id = id, Name = "Temp", Position = 1 });
        await _repo.DeleteAsync(id);

        var fetched = await _repo.GetByIdAsync(id);
        Assert.Null(fetched);
    }

    [Fact]
    public async Task AssignAndRevokeAccount()
    {
        var roleId = Guid.NewGuid();
        await _repo.AddAsync(new RoleEntity { Id = roleId, Name = "Mod", Position = 50 });

        await _repo.AssignToAccountAsync("user1", roleId);
        var accounts = await _repo.GetAccountsWithRoleAsync(roleId);
        Assert.Single(accounts);
        Assert.Equal("user1", accounts[0]);

        await _repo.RevokeFromAccountAsync("user1", roleId);
        accounts = await _repo.GetAccountsWithRoleAsync(roleId);
        Assert.Empty(accounts);
    }

    [Fact]
    public async Task GetRolesForAccountAsync_ReturnsAssignedRoles()
    {
        var role1 = new RoleEntity { Id = Guid.NewGuid(), Name = "Role1", Position = 1 };
        var role2 = new RoleEntity { Id = Guid.NewGuid(), Name = "Role2", Position = 2 };
        await _repo.AddAsync(role1);
        await _repo.AddAsync(role2);

        await _repo.AssignToAccountAsync("user1", role1.Id);
        await _repo.AssignToAccountAsync("user1", role2.Id);

        var roles = await _repo.GetRolesForAccountAsync("user1");
        Assert.Equal(2, roles.Count);
    }

    [Fact]
    public async Task AssignToAccountAsync_IsIdempotent()
    {
        var roleId = Guid.NewGuid();
        await _repo.AddAsync(new RoleEntity { Id = roleId, Name = "Mod", Position = 50 });

        await _repo.AssignToAccountAsync("user1", roleId);
        await _repo.AssignToAccountAsync("user1", roleId); // duplicate

        var accounts = await _repo.GetAccountsWithRoleAsync(roleId);
        Assert.Single(accounts);
    }
}
