using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public sealed class ExceptionLogConfiguration : IEntityTypeConfiguration<ExceptionLog>
{
    public void Configure(EntityTypeBuilder<ExceptionLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OccurredOnUtc).IsRequired();
        builder.Property(x => x.ExceptionType).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        builder.Property(x => x.Source).HasMaxLength(512);
        builder.Property(x => x.CaptureKind).HasMaxLength(64);
        builder.Property(x => x.RequestPath).HasMaxLength(2048);
        builder.Property(x => x.RequestMethod).HasMaxLength(16);
        builder.Property(x => x.TraceId).HasMaxLength(128);
        builder.Property(x => x.UserIdentifier).HasMaxLength(256);
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");

        builder.HasIndex(x => x.OccurredOnUtc);
        builder.HasIndex(x => x.ExceptionType);
    }
}
