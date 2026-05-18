using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Career;

public sealed class EmployeeRoleProfileConfiguration : IEntityTypeConfiguration<EmployeeRoleProfile>
{
    public void Configure(EntityTypeBuilder<EmployeeRoleProfile> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.TenantId, x.EmployeeId })
            .IsUnique()
            .HasFilter("is_active = true");
        builder.HasIndex(x => new { x.TenantId, x.RoleProfileId });

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
            .HasOne(x => x.RoleProfile)
            .WithMany(x => x.EmployeeAssignments)
            .HasForeignKey(x => x.RoleProfileId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
