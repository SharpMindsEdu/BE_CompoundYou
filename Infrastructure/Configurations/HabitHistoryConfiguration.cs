using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class HabitHistoryConfiguration : IEntityTypeConfiguration<HabitHistory>
{
    public void Configure(EntityTypeBuilder<HabitHistory> builder)
    {
        builder.HasKey(x => x.Id);
        builder
            .HasOne(x => x.Habit)
            .WithMany(x => x.History)
            .HasForeignKey(x => x.HabitId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.User)
            .WithMany(x => x.HabitHistory)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.HabitTime)
            .WithMany()
            .HasForeignKey(x => x.HabitTimeId)
            .OnDelete(DeleteBehavior.Cascade)
            .IsRequired(false);
    }
}
