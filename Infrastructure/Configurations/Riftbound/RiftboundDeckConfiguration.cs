using Domain.Entities;
using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundDeckConfiguration : IEntityTypeConfiguration<RiftboundDeck>
{
    public void Configure(EntityTypeBuilder<RiftboundDeck> builder)
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Colors).HasColumnType("text[]");

        builder
            .HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasOne(x => x.Legend)
            .WithMany()
            .HasForeignKey(x => x.LegendId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.Champion)
            .WithMany()
            .HasForeignKey(x => x.ChampionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasMany(x => x.Cards)
            .WithOne(x => x.Deck)
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Shares)
            .WithOne(x => x.Deck)
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Ratings)
            .WithOne(x => x.Deck)
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder
            .HasMany(x => x.Comments)
            .WithOne(x => x.Deck)
            .HasForeignKey(x => x.DeckId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
