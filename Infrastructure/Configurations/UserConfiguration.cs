using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).HasMaxLength(255);

        builder.HasIndex(x => x.Email).IsUnique();

        builder.HasIndex(x => x.PhoneNumber).IsUnique();

        builder
            .HasGeneratedTsVectorColumn(
                x => x.DisplayNameSearchVector,
                "german",
                x => x.DisplayName
            )
            .HasIndex(x => x.DisplayNameSearchVector)
            .HasMethod("GIN");
    }
}
