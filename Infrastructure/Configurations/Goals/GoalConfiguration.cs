using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Goals;

public sealed class GoalConfiguration : IEntityTypeConfiguration<Goal>
{
    public void Configure(EntityTypeBuilder<Goal> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title).HasMaxLength(300).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(4000);
        builder.Property(x => x.Period).HasConversion<int>();
        builder.Property(x => x.TargetType).HasConversion<int>();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.TargetValue).HasPrecision(18, 4);
        builder.Property(x => x.CurrentValue).HasPrecision(18, 4);

        builder.HasIndex(x => new { x.TenantId, x.EmployeeId, x.Status });
        builder.HasIndex(x => x.TargetSkillId);
        builder.HasIndex(x => x.AuthorEmployeeId);
        builder.HasIndex(x => x.DueOn);

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
            .HasOne(x => x.AuthorEmployee)
            .WithMany()
            .HasForeignKey(x => x.AuthorEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.TargetSkill)
            .WithMany()
            .HasForeignKey(x => x.TargetSkillId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
