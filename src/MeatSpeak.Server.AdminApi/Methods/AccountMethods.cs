namespace MeatSpeak.Server.AdminApi.Methods;

using System.Text.Json;
using MeatSpeak.Server.AdminApi.Auth;
using MeatSpeak.Server.Data.Entities;
using MeatSpeak.Server.Data.Repositories;

public sealed class AccountCreateMethod : IAdminMethod
{
    private readonly IUserAccountRepository _repo;
    public string Name => "account.create";

    public AccountCreateMethod(IUserAccountRepository repo) => _repo = repo;

    public async Task<object?> ExecuteAsync(JsonElement? parameters, CancellationToken ct = default)
    {
        var p = AdminParamHelper.Require(parameters);
        var account = AdminParamHelper.RequireString(p, "account");
        var password = AdminParamHelper.RequireString(p, "password");

        var existing = await _repo.GetByAccountAsync(account, ct);
        if (existing != null)
            return new { error = "account_exists", message = "Account already exists" };

        var hash = PasswordHasher.HashPassword(password);
        await _repo.AddAsync(new UserAccountEntity
        {
            Id = Guid.NewGuid(),
            Account = account,
            PasswordHash = hash,
        }, ct);

        return new { success = true, account };
    }
}
