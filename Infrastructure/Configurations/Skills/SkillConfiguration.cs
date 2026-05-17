using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Skills;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);

        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        builder.HasIndex(x => x.SkillCategoryId);
        builder.HasIndex(x => x.ParentSkillId);

        builder
            .HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.SkillCategory)
            .WithMany(x => x.Skills)
            .HasForeignKey(x => x.SkillCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.ParentSkill)
            .WithMany(x => x.ChildSkills)
            .HasForeignKey(x => x.ParentSkillId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
