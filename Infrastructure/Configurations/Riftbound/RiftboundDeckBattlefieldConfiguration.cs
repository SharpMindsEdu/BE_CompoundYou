using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundDeckBattlefieldConfiguration
    : IEntityTypeConfiguration<RiftboundDeckBattlefield>
{
    public void Configure(EntityTypeBuilder<RiftboundDeckBattlefield> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.DeckId, x.CardId }).IsUnique();

        builder
            .HasOne(x => x.Deck)
            .WithMany(x => x.Battlefields)
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Card)
            .WithMany()
            .HasForeignKey(x => x.CardId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
