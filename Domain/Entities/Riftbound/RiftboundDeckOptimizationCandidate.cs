namespace Domain.Entities.Riftbound;

public class RiftboundDeckOptimizationCandidate
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public long DeckId { get; set; }
    public long LegendId { get; set; }
    public int Generation { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Draws { get; set; }
    public int GamesPlayed { get; set; }
    public decimal WinRate { get; set; }
    public decimal SonnebornBerger { get; set; }
    public decimal HeadToHeadScore { get; set; }
    public int RankGlobal { get; set; }
    public int RankInLegend { get; set; }

    public RiftboundDeckOptimizationRun? Run { get; set; }
    public RiftboundDeck? Deck { get; set; }
}
