namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MeatSpeak.Server.Data.Entities;

public sealed class ChatLogEntityConfiguration : IEntityTypeConfiguration<ChatLogEntity>
{
    public void Configure(EntityTypeBuilder<ChatLogEntity> builder)
    {
        builder.ToTable("chat_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.ChannelName).HasMaxLength(200);
        builder.Property(e => e.Target).HasMaxLength(64);
        builder.Property(e => e.Sender).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Message).HasMaxLength(2048).IsRequired();
        builder.Property(e => e.MessageType).HasMaxLength(16).IsRequired();

        builder.Property(e => e.SentAt).HasConversion(new DateTimeOffsetToBinaryConverter());

        builder.HasIndex(e => e.ChannelName);
        builder.HasIndex(e => e.Sender);
        builder.HasIndex(e => e.SentAt);
    }
}
