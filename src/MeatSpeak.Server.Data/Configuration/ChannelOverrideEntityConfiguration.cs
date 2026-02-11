namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeatSpeak.Server.Data.Entities;

public sealed class ChannelOverrideEntityConfiguration : IEntityTypeConfiguration<ChannelOverrideEntity>
{
    public void Configure(EntityTypeBuilder<ChannelOverrideEntity> builder)
    {
        builder.ToTable("channel_overrides");
        builder.HasKey(e => new { e.RoleId, e.ChannelName });
        builder.Property(e => e.ChannelName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Allow);
        builder.Property(e => e.Deny);
        builder.HasOne(e => e.Role).WithMany(r => r.ChannelOverrides).HasForeignKey(e => e.RoleId);
    }
}
