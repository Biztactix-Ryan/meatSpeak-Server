namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MeatSpeak.Server.Data.Entities;

public sealed class ChannelEntityConfiguration : IEntityTypeConfiguration<ChannelEntity>
{
    public void Configure(EntityTypeBuilder<ChannelEntity> builder)
    {
        builder.ToTable("channels");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Topic).HasMaxLength(512);
        builder.Property(e => e.TopicSetBy).HasMaxLength(64);
        builder.Property(e => e.Key).HasMaxLength(64);
        builder.Property(e => e.Modes).HasMaxLength(32);

        var dtoConverter = new DateTimeOffsetToBinaryConverter();
        builder.Property(e => e.CreatedAt).HasConversion(dtoConverter);
        builder.Property(e => e.TopicSetAt).HasConversion(new ValueConverter<DateTimeOffset?, long?>(
            v => v.HasValue ? v.Value.ToUniversalTime().Ticks : null,
            v => v.HasValue ? new DateTimeOffset(v.Value, TimeSpan.Zero) : null));

        builder.HasIndex(e => e.Name).IsUnique();
    }
}
