namespace Domain.Entities.Riftbound;

public class RiftboundDeckShare
{
    public long Id { get; set; }
    public long DeckId { get; set; }
    public long UserId { get; set; }

    public RiftboundDeck? Deck { get; set; }
}
