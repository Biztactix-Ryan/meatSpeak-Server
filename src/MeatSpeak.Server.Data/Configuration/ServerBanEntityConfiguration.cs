namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeatSpeak.Server.Data.Entities;

public sealed class ServerBanEntityConfiguration : IEntityTypeConfiguration<ServerBanEntity>
{
    public void Configure(EntityTypeBuilder<ServerBanEntity> builder)
    {
        builder.ToTable("server_bans");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Mask).HasMaxLength(256).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(512);
        builder.Property(e => e.SetBy).HasMaxLength(64).IsRequired();
        builder.Property(e => e.SetAt);
        builder.Property(e => e.ExpiresAt);
        builder.HasIndex(e => e.Mask);
    }
}
