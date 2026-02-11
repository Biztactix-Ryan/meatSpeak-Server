namespace MeatSpeak.Server.Data.Repositories;

using MeatSpeak.Server.Data.Entities;

public interface IRoleRepository
{
    Task<RoleEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<RoleEntity?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<RoleEntity>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<RoleEntity>> GetRolesForAccountAsync(string account, CancellationToken ct = default);
    Task AddAsync(RoleEntity role, CancellationToken ct = default);
    Task UpdateAsync(RoleEntity role, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task AssignToAccountAsync(string account, Guid roleId, CancellationToken ct = default);
    Task RevokeFromAccountAsync(string account, Guid roleId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAccountsWithRoleAsync(Guid roleId, CancellationToken ct = default);
}
