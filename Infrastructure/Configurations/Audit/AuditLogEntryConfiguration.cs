using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Audit;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Action).HasMaxLength(120).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(200).IsRequired();
        builder.Property(x => x.MetadataJson).HasColumnType("jsonb");
        builder.Property(x => x.OccurredOn).IsRequired();

        builder.HasIndex(x => new { x.TenantId, x.OccurredOn });
        builder.HasIndex(x => new { x.TenantId, x.EntityType, x.EntityId });
        builder.HasIndex(x => x.ActorUserId);
    }
}
