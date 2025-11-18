namespace Domain.Entities.Riftbound;

public class RiftboundDeckCard
{
    public long Id { get; set; }
    public long DeckId { get; set; }
    public long CardId { get; set; }
    public int Quantity { get; set; }

    public RiftboundDeck? Deck { get; set; }
    public RiftboundCard? Card { get; set; }
}
