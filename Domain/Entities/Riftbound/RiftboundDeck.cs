using Domain.Entities;

namespace Domain.Entities.Riftbound;

public class RiftboundDeck : TrackedEntity
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public bool IsPublic { get; set; }
    public List<string> Colors { get; set; } = [];
    public long OwnerId { get; set; }
    public long LegendId { get; set; }
    public long ChampionId { get; set; }

    public User? Owner { get; set; }
    public RiftboundCard? Legend { get; set; }
    public RiftboundCard? Champion { get; set; }
    public List<RiftboundDeckCard> Cards { get; set; } = [];
    public List<RiftboundDeckShare> Shares { get; set; } = [];
    public List<RiftboundDeckRating> Ratings { get; set; } = [];
    public List<RiftboundDeckComment> Comments { get; set; } = [];
}
