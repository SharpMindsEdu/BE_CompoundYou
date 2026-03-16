using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundDeckOptimizationRunConfiguration
    : IEntityTypeConfiguration<RiftboundDeckOptimizationRun>
{
    public void Configure(EntityTypeBuilder<RiftboundDeckOptimizationRun> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(30);
        builder.Property(x => x.ProgressPercent).HasPrecision(5, 2);
        builder.Property(x => x.ErrorMessage).HasMaxLength(4000);

        builder
            .HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(x => x.Candidates)
            .WithOne(x => x.Run)
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Matchups)
            .WithOne(x => x.Run)
            .HasForeignKey(x => x.RunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.RequestedByUserId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.CreatedOn);
    }
}
