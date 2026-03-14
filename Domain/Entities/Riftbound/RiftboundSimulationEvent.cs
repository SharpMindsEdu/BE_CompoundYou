using Domain.Entities;

namespace Domain.Entities.Riftbound;

public class RiftboundSimulationEvent : TrackedEntity
{
    public long Id { get; set; }
    public long SimulationRunId { get; set; }
    public int Sequence { get; set; }
    public string EventType { get; set; } = "";
    public string PayloadJson { get; set; } = "{}";

    public RiftboundSimulationRun? SimulationRun { get; set; }
}
