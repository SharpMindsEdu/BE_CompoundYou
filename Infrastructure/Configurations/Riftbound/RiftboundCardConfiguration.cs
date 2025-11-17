using Domain.Entities.Riftbound;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations.Riftbound;

public class RiftboundCardConfiguration : IEntityTypeConfiguration<RiftboundCard>
{
    public void Configure(EntityTypeBuilder<RiftboundCard> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.ReferenceId).IsUnique();
        builder.Property(x => x.ReferenceId).IsRequired();
    }
}
