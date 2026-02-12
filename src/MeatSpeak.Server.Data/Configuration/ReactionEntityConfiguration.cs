namespace MeatSpeak.Server.Data.Configuration;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MeatSpeak.Server.Data.Entities;

public sealed class ReactionEntityConfiguration : IEntityTypeConfiguration<ReactionEntity>
{
    public void Configure(EntityTypeBuilder<ReactionEntity> builder)
    {
        builder.ToTable("reactions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd();
        builder.Property(e => e.MsgId).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Sender).HasMaxLength(64).IsRequired();
        builder.Property(e => e.Reaction).HasMaxLength(64).IsRequired();
        builder.Property(e => e.CreatedAt).HasConversion(new DateTimeOffsetToBinaryConverter());

        builder.HasIndex(e => e.MsgId);
        builder.HasIndex(e => new { e.MsgId, e.Sender, e.Reaction }).IsUnique();
    }
}
