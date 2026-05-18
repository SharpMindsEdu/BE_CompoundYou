using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Career;

public sealed class RoleProfileSkillRequirementConfiguration
    : IEntityTypeConfiguration<RoleProfileSkillRequirement>
{
    public void Configure(EntityTypeBuilder<RoleProfileSkillRequirement> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Weight).HasPrecision(6, 2);

        builder.HasIndex(x => new { x.RoleProfileId, x.SkillId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.SkillId });

        builder
            .HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.RoleProfile)
            .WithMany(x => x.SkillRequirements)
            .HasForeignKey(x => x.RoleProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.RequiredSkillLevel)
            .WithMany()
            .HasForeignKey(x => x.RequiredSkillLevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
