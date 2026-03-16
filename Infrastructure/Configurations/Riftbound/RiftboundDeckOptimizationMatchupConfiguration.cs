using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundDeckOptimizationMatchupConfiguration
    : IEntityTypeConfiguration<RiftboundDeckOptimizationMatchup>
{
    public void Configure(EntityTypeBuilder<RiftboundDeckOptimizationMatchup> builder)
    {
        builder.HasKey(x => x.Id);

        builder
            .HasOne(x => x.Run)
            .WithMany(x => x.Matchups)
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.DeckA)
            .WithMany()
            .HasForeignKey(x => x.DeckAId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.DeckB)
            .WithMany()
            .HasForeignKey(x => x.DeckBId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.RunId, x.Generation, x.DeckAId, x.DeckBId }).IsUnique();
        builder.HasIndex(x => new { x.RunId, x.Generation });
    }
}
