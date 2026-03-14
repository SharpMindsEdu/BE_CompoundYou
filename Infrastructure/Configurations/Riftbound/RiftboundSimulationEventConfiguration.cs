using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundSimulationEventConfiguration
    : IEntityTypeConfiguration<RiftboundSimulationEvent>
{
    public void Configure(EntityTypeBuilder<RiftboundSimulationEvent> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnType("jsonb");

        builder
            .HasOne(x => x.SimulationRun)
            .WithMany(x => x.Events)
            .HasForeignKey(x => x.SimulationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.SimulationRunId, x.Sequence }).IsUnique();
    }
}
