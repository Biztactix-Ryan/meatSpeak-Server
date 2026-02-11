namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MeatSpeak.Server.Data.Entities;

public sealed class UserHistoryEntityConfiguration : IEntityTypeConfiguration<UserHistoryEntity>
{
    public void Configure(EntityTypeBuilder<UserHistoryEntity> builder)
    {
        builder.ToTable("user_history");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.Nickname).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Username).HasMaxLength(64);
        builder.Property(e => e.Hostname).HasMaxLength(256);
        builder.Property(e => e.Account).HasMaxLength(64);
        builder.Property(e => e.QuitReason).HasMaxLength(256);

        var dtoConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(e => e.ConnectedAt).HasConversion(dtoConverter);
        builder.Property(e => e.DisconnectedAt).HasConversion(new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.ToUniversalTime().Ticks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null));

        builder.HasIndex(e => e.Nickname);
        builder.HasIndex(e => e.Account);
    }
}
