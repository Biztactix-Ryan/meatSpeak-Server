namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeatSpeak.Server.Data.Entities;

public sealed class RoleEntityConfiguration : IEntityTypeConfiguration<RoleEntity>
{
    public void Configure(EntityTypeBuilder<RoleEntity> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Name).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Position);
        builder.Property(e => e.ServerPermissions);
        builder.Property(e => e.DefaultChannelPermissions);
        builder.Property(e => e.CreatedAt);
        builder.HasIndex(e => e.Name).IsUnique();
    }
}
