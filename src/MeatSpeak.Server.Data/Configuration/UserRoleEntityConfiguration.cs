namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeatSpeak.Server.Data.Entities;

public sealed class UserRoleEntityConfiguration : IEntityTypeConfiguration<UserRoleEntity>
{
    public void Configure(EntityTypeBuilder<UserRoleEntity> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(e => new { e.Account, e.RoleId });
        builder.Property(e => e.Account).HasMaxLength(64).IsRequired();
        builder.Property(e => e.AssignedAt);
        builder.HasOne(e => e.Role).WithMany(r => r.UserRoles).HasForeignKey(e => e.RoleId);
    }
}
