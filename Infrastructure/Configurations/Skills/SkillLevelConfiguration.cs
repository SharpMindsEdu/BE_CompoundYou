using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Skills;

public sealed class SkillLevelConfiguration : IEntityTypeConfiguration<SkillLevel>
{
    public void Configure(EntityTypeBuilder<SkillLevel> builder)
    {
        builder.ToTable(t =>
            t.HasCheckConstraint(
                "ck_skill_level_tenant_wide_or_inactive",
                "skill_id IS NULL OR NOT is_active"
            )
        );

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);

        builder
            .HasIndex(x => new { x.SkillId, x.Order })
            .IsUnique()
            .HasFilter("skill_id IS NOT NULL AND is_active");

        builder
            .HasIndex(x => new { x.TenantId, x.Order })
            .IsUnique()
            .HasFilter("skill_id IS NULL AND tenant_id IS NOT NULL AND is_active");

        builder
            .HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne<Skill>()
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
