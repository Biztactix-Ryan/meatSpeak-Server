namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MeatSpeak.Server.Data.Entities;

public sealed class TopicHistoryEntityConfiguration : IEntityTypeConfiguration<TopicHistoryEntity>
{
    public void Configure(EntityTypeBuilder<TopicHistoryEntity> builder)
    {
        builder.ToTable("topic_history");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.ChannelName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Topic).HasMaxLength(512).IsRequired();
        builder.Property(e => e.SetBy).HasMaxLength(64).IsRequired();

        builder.Property(e => e.SetAt).HasConversion(new DateTimeOffsetToBinaryConverter());

        builder.HasIndex(e => e.ChannelName);
    }
}
