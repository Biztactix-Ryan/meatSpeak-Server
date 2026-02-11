namespace MeatSpeak.Server.Data;

using Microsoft.EntityFrameworkCore;
using MeatSpeak.Server.Permissions;
using MeatSpeak.Server.Data.Entities;

public sealed class DatabaseSeeder
{
    private readonly MeatSpeakDbContext _db;

    public DatabaseSeeder(MeatSpeakDbContext db) => _db = db;

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await _db.Database.EnsureCreatedAsync(ct);

        if (await _db.Roles.AnyAsync(ct))
            return;

        var everyone = BuiltInRoles.Everyone();
        var moderator = BuiltInRoles.Moderator();
        var admin = BuiltInRoles.Admin();

        _db.Roles.AddRange(
            new RoleEntity
            {
                Id = everyone.Id,
                Name = everyone.Name,
                Position = everyone.Position,
                ServerPermissions = (ulong)everyone.ServerPermissions,
                DefaultChannelPermissions = (ulong)everyone.DefaultChannelPermissions,
            },
            new RoleEntity
            {
                Id = moderator.Id,
                Name = moderator.Name,
                Position = moderator.Position,
                ServerPermissions = (ulong)moderator.ServerPermissions,
                DefaultChannelPermissions = (ulong)moderator.DefaultChannelPermissions,
            },
            new RoleEntity
            {
                Id = admin.Id,
                Name = admin.Name,
                Position = admin.Position,
                ServerPermissions = (ulong)admin.ServerPermissions,
                DefaultChannelPermissions = (ulong)admin.DefaultChannelPermissions,
            });

        await _db.SaveChangesAsync(ct);
    }
}
