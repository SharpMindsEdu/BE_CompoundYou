using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundDeckCardConfiguration : IEntityTypeConfiguration<RiftboundDeckCard>
{
    public void Configure(EntityTypeBuilder<RiftboundDeckCard> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).IsRequired();
        builder.HasIndex(x => new { x.DeckId, x.CardId }).IsUnique();
    }
}
