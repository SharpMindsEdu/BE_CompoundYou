using Domain.Entities;

namespace Domain.Entities.Riftbound;

public class RiftboundSimulationRun : TrackedEntity
{
    public long Id { get; set; }
    public long RequestedByUserId { get; set; }
    public long ChallengerDeckId { get; set; }
    public long OpponentDeckId { get; set; }
    public long Seed { get; set; }
    public string RulesetVersion { get; set; } = "";
    public string Mode { get; set; } = "1v1-duel";
    public string ChallengerPolicy { get; set; } = "heuristic";
    public string OpponentPolicy { get; set; } = "heuristic";
    public string Status { get; set; } = "running";
    public int? WinnerPlayerIndex { get; set; }
    public string ScoreSummaryJson { get; set; } = "{}";
    public string SnapshotJson { get; set; } = "{}";

    public RiftboundDeck? ChallengerDeck { get; set; }
    public RiftboundDeck? OpponentDeck { get; set; }
    public User? RequestedByUser { get; set; }
    public List<RiftboundSimulationEvent> Events { get; set; } = [];
}
