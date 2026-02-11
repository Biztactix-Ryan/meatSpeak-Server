namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MeatSpeak.Server.Data.Entities;

public sealed class AuditLogEntityConfiguration : IEntityTypeConfiguration<AuditLogEntity>
{
    public void Configure(EntityTypeBuilder<AuditLogEntity> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.Action).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Actor).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Target).HasMaxLength(256);
        builder.Property(e => e.Details).HasMaxLength(2048);

        // Store DateTimeOffset as ticks (long) for SQLite compatibility
        builder.Property(e => e.Timestamp).HasConversion(new DateTimeOffsetToBinaryConverter());
        builder.HasIndex(e => e.Timestamp);
        builder.HasIndex(e => e.Actor);
    }
}
