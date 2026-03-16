using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundDeckSideboardCardConfiguration
    : IEntityTypeConfiguration<RiftboundDeckSideboardCard>
{
    public void Configure(EntityTypeBuilder<RiftboundDeckSideboardCard> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).IsRequired();
        builder.HasIndex(x => new { x.DeckId, x.CardId }).IsUnique();

        builder
            .HasOne(x => x.Deck)
            .WithMany(x => x.SideboardCards)
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Card)
            .WithMany()
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
