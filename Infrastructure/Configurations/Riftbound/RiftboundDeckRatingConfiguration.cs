using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundDeckRatingConfiguration : IEntityTypeConfiguration<RiftboundDeckRating>
{
    public void Configure(EntityTypeBuilder<RiftboundDeckRating> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Value).IsRequired();
        builder.HasIndex(x => new { x.DeckId, x.UserId }).IsUnique();
    }
}
