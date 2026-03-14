using Domain.Entities.Riftbound;

namespace Application.Features.Riftbound.Simulation.Services;

public sealed record RiftboundDeckSimulationReadiness(
    bool IsSimulationReady,
    IReadOnlyCollection<string> ValidationIssues,
    IReadOnlyCollection<string> UnsupportedCards
);

public interface IRiftboundDeckSimulationReadinessService
{
    RiftboundDeckSimulationReadiness Evaluate(RiftboundDeck deck);
}
