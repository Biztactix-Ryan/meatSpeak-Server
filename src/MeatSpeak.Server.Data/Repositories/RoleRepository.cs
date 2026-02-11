namespace MeatSpeak.Server.Data.Repositories;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Data.Entities;

public sealed class RoleRepository : IRoleRepository
{
    private readonly MeatSpeakDbContext _db;

    public RoleRepository(MeatSpeakDbContext db) => _db = db;

    public async Task<RoleEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.Roles.FindAsync(new object[] { id }, ct);

    public async Task<RoleEntity?> GetByNameAsync(string name, CancellationToken ct = default)
        => await _db.Roles.FirstOrDefaultAsync(r => r.Name == name, ct);

    public async Task<IReadOnlyList<RoleEntity>> GetAllAsync(CancellationToken ct = default)
        => await _db.Roles.OrderBy(r => r.Position).ToListAsync(ct);

    public async Task<IReadOnlyList<RoleEntity>> GetRolesForAccountAsync(string account, CancellationToken ct = default)
        => await _db.UserRoles
            .Where(ur => ur.Account == account)
            .Include(ur => ur.Role)
            .Select(ur => ur.Role!)
            .OrderBy(r => r.Position)
            .ToListAsync(ct);

    public async Task AddAsync(RoleEntity role, CancellationToken ct = default)
    {
        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(RoleEntity role, CancellationToken ct = default)
    {
        _db.Roles.Update(role);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles.FindAsync(new object[] { id }, ct);
        if (role != null)
        {
            _db.Roles.Remove(role);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task AssignToAccountAsync(string account, Guid roleId, CancellationToken ct = default)
    {
        var exists = await _db.UserRoles.AnyAsync(ur => ur.Account == account && ur.RoleId == roleId, ct);
        if (!exists)
        {
            _db.UserRoles.Add(new UserRoleEntity { Account = account, RoleId = roleId });
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeFromAccountAsync(string account, Guid roleId, CancellationToken ct = default)
    {
        var ur = await _db.UserRoles.FirstOrDefaultAsync(x => x.Account == account && x.RoleId == roleId, ct);
        if (ur != null)
        {
            _db.UserRoles.Remove(ur);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<string>> GetAccountsWithRoleAsync(Guid roleId, CancellationToken ct = default)
        => await _db.UserRoles.Where(ur => ur.RoleId == roleId).Select(ur => ur.Account).ToListAsync(ct);
}
