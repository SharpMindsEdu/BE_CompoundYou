using Domain.Entities;

namespace Domain.Entities.Riftbound;

public class RiftboundDeckRating : TrackedEntity
{
    public long Id { get; set; }
    public long DeckId { get; set; }
    public long UserId { get; set; }
    public int Value { get; set; }

    public RiftboundDeck? Deck { get; set; }
}
