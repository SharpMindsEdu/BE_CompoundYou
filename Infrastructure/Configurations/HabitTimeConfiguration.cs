using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class HabitTimeConfiguration : IEntityTypeConfiguration<HabitTime>
{
    public void Configure(EntityTypeBuilder<HabitTime> builder)
    {
        builder.HasKey(x => x.Id);
        builder
            .HasOne(x => x.Habit)
            .WithMany(x => x.Times)
            .HasForeignKey(x => x.HabitId)
            .OnDelete(DeleteBehavior.Cascade);
        builder
            .HasOne(x => x.User)
            .WithMany(x => x.HabitTimes)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
