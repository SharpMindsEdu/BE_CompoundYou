using Domain.Entities;

namespace Domain.Entities.Riftbound;

public class RiftboundDeckOptimizationRun : TrackedEntity
{
    public long Id { get; set; }
    public long RequestedByUserId { get; set; }
    public string Status { get; set; } = "queued";
    public long Seed { get; set; }
    public int PopulationSize { get; set; }
    public int Generations { get; set; }
    public int SeedsPerMatch { get; set; }
    public int MaxAutoplaySteps { get; set; }
    public int CurrentGeneration { get; set; }
    public decimal ProgressPercent { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset? StartedOn { get; set; }
    public DateTimeOffset? CompletedOn { get; set; }

    public User? RequestedByUser { get; set; }
    public List<RiftboundDeckOptimizationCandidate> Candidates { get; set; } = [];
    public List<RiftboundDeckOptimizationMatchup> Matchups { get; set; } = [];
}
