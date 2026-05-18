using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Career;

public sealed class CareerLevelConfiguration : IEntityTypeConfiguration<CareerLevel>
{
    public void Configure(EntityTypeBuilder<CareerLevel> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.HasIndex(x => new { x.JobFamilyId, x.Order }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.JobFamilyId });

        builder
            .HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.JobFamily)
            .WithMany(x => x.CareerLevels)
            .HasForeignKey(x => x.JobFamilyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
