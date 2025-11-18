using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundDeckShareConfiguration : IEntityTypeConfiguration<RiftboundDeckShare>
{
    public void Configure(EntityTypeBuilder<RiftboundDeckShare> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.DeckId, x.UserId }).IsUnique();
    }
}
