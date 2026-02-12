namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeatSpeak.Server.Data.Entities;

public sealed class UserAccountEntityConfiguration : IEntityTypeConfiguration<UserAccountEntity>
{
    public void Configure(EntityTypeBuilder<UserAccountEntity> builder)
    {
        builder.ToTable("user_accounts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Account).HasMaxLength(64).IsRequired();
        builder.HasIndex(e => e.Account).IsUnique();
        builder.Property(e => e.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(e => e.CreatedAt);
        builder.Property(e => e.LastLogin);
        builder.Property(e => e.Disabled);
    }
}
