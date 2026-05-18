using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Career;

public sealed class RoleProfileConfiguration : IEntityTypeConfiguration<RoleProfile>
{
    public void Configure(EntityTypeBuilder<RoleProfile> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(180).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1500);

        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        builder.HasIndex(x => new { x.JobFamilyId, x.CareerLevelId });
        builder.HasIndex(x => new { x.TenantId, x.IsActive });

        builder
            .HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.JobFamily)
            .WithMany(x => x.RoleProfiles)
            .HasForeignKey(x => x.JobFamilyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.CareerLevel)
            .WithMany(x => x.RoleProfiles)
            .HasForeignKey(x => x.CareerLevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
