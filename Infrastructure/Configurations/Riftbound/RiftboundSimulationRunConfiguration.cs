using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundSimulationRunConfiguration : IEntityTypeConfiguration<RiftboundSimulationRun>
{
    public void Configure(EntityTypeBuilder<RiftboundSimulationRun> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.RulesetVersion).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Mode).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ChallengerPolicy).IsRequired().HasMaxLength(50);
        builder.Property(x => x.OpponentPolicy).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ScoreSummaryJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.SnapshotJson).IsRequired().HasColumnType("jsonb");

        builder
            .HasOne(x => x.ChallengerDeck)
            .WithMany()
            .HasForeignKey(x => x.ChallengerDeckId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.OpponentDeck)
            .WithMany()
            .HasForeignKey(x => x.OpponentDeckId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(x => x.Events)
            .WithOne(x => x.SimulationRun)
            .HasForeignKey(x => x.SimulationRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RequestedByUserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedOn);
    }
}
