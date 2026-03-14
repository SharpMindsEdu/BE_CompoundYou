using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Engine;

public interface IRiftboundSimulationEngine
{
    GameSession CreateSession(RiftboundSimulationEngineSetup setup);
    IReadOnlyCollection<RiftboundLegalAction> GetLegalActions(GameSession session);
    RiftboundSimulationEngineResult ApplyAction(GameSession session, string actionId);
}
