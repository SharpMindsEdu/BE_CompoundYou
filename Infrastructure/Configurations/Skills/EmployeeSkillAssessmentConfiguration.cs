using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Skills;

public sealed class EmployeeSkillAssessmentConfiguration : IEntityTypeConfiguration<EmployeeSkillAssessment>
{
    public void Configure(EntityTypeBuilder<EmployeeSkillAssessment> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.Evidence).HasMaxLength(4000);

        builder.HasIndex(x => new { x.EmployeeId, x.SkillId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status });
        builder.HasIndex(x => x.ValidatedByEmployeeId);

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
            .HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.ClaimedSkillLevel)
            .WithMany()
            .HasForeignKey(x => x.ClaimedSkillLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.ValidatedSkillLevel)
            .WithMany()
            .HasForeignKey(x => x.ValidatedSkillLevelId)
            .OnDelete(DeleteBehavior.SetNull);

        builder
            .HasOne(x => x.ValidatedByEmployee)
            .WithMany()
            .HasForeignKey(x => x.ValidatedByEmployeeId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
