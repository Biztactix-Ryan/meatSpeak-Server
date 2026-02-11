namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
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

        // Store DateTimeOffset as ticks (long) for SQLite compatibility
        var dtoConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(e => e.SetAt).HasConversion(dtoConverter);
        builder.Property(e => e.ExpiresAt).HasConversion(new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.ToUniversalTime().Ticks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null));

        builder.HasIndex(e => e.Mask);
    }
}
