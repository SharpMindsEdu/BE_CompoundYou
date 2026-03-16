using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundDeckOptimizationCandidateConfiguration
    : IEntityTypeConfiguration<RiftboundDeckOptimizationCandidate>
{
    public void Configure(EntityTypeBuilder<RiftboundDeckOptimizationCandidate> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.WinRate).HasPrecision(10, 6);
        builder.Property(x => x.SonnebornBerger).HasPrecision(12, 6);
        builder.Property(x => x.HeadToHeadScore).HasPrecision(12, 6);

        builder
            .HasOne(x => x.Run)
            .WithMany(x => x.Candidates)
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Deck)
            .WithMany()
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RunId, x.Generation, x.DeckId }).IsUnique();
        builder.HasIndex(x => new { x.RunId, x.Generation, x.RankGlobal });
        builder.HasIndex(x => new { x.RunId, x.Generation, x.LegendId, x.RankInLegend });
    }
}
