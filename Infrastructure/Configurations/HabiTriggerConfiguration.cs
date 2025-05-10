using Domain;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class HabitTriggerConfiguration : IEntityTypeConfiguration<HabitTrigger>
{

    public void Configure(EntityTypeBuilder<HabitTrigger> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Habit)
            .WithMany(x => x.Triggers)
            .HasForeignKey(x => x.HabitId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}