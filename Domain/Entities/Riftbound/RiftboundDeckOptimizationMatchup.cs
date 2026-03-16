namespace Domain.Entities.Riftbound;

public class RiftboundDeckOptimizationMatchup
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public int Generation { get; set; }
    public long DeckAId { get; set; }
    public long DeckBId { get; set; }
    public int DeckAWins { get; set; }
    public int DeckBWins { get; set; }
    public int Draws { get; set; }
    public int GamesPlayed { get; set; }

    public RiftboundDeckOptimizationRun? Run { get; set; }
    public RiftboundDeck? DeckA { get; set; }
    public RiftboundDeck? DeckB { get; set; }
}
