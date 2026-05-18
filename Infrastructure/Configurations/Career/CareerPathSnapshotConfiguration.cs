using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Career;

public sealed class CareerPathSnapshotConfiguration : IEntityTypeConfiguration<CareerPathSnapshot>
{
    public void Configure(EntityTypeBuilder<CareerPathSnapshot> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Band).HasConversion<int?>();

        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.ScoredOn });
        builder.HasIndex(x => new { x.TenantId, x.TargetRoleProfileId });

        builder
            .HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.CurrentRoleProfile)
            .WithMany()
            .HasForeignKey(x => x.CurrentRoleProfileId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(x => x.TargetRoleProfile)
            .WithMany()
            .HasForeignKey(x => x.TargetRoleProfileId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
