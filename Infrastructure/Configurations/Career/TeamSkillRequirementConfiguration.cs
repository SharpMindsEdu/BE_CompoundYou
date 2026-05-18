using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Career;

public sealed class TeamSkillRequirementConfiguration : IEntityTypeConfiguration<TeamSkillRequirement>
{
    public void Configure(EntityTypeBuilder<TeamSkillRequirement> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.TeamId, x.SkillId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.TeamId });

        builder
            .HasOne(x => x.Tenant)
            .WithMany()
            .HasForeignKey(x => x.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Team)
            .WithMany()
            .HasForeignKey(x => x.TeamId)
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
