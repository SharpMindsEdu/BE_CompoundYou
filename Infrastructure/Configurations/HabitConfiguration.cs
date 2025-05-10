using Domain;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class HabitConfiguration : IEntityTypeConfiguration<Habit>
{
    public void Configure(EntityTypeBuilder<Habit> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasOne(x => x.Base)
            .WithMany(x => x.Preparations)
            .HasForeignKey(x => x.HabitPreparationId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(x => x.User)
            .WithMany(x => x.Habits)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder
            .HasGeneratedTsVectorColumn(
                x => x.TitleSearchVector,
                "german",
                x => x.Title
            )
            .HasIndex(x => x.TitleSearchVector)
            .HasMethod("GIN");
    }
}