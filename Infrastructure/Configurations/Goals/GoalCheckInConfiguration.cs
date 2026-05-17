using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Goals;

public sealed class GoalCheckInConfiguration : IEntityTypeConfiguration<GoalCheckIn>
{
    public void Configure(EntityTypeBuilder<GoalCheckIn> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Note).HasMaxLength(4000);
        builder.Property(x => x.ProgressValue).HasPrecision(18, 4);

        builder.HasIndex(x => new { x.GoalId, x.CreatedOn });
        builder.HasIndex(x => x.AuthorEmployeeId);

        builder
            .HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Goal)
            .WithMany(x => x.CheckIns)
            .HasForeignKey(x => x.GoalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.AuthorEmployee)
            .WithMany()
            .HasForeignKey(x => x.AuthorEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
