using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Engine;

public sealed record RiftboundSimulationEngineSetup(
    long SimulationId,
    long RequestedByUserId,
    long Seed,
    string RulesetVersion,
    RiftboundDeck ChallengerDeck,
    RiftboundDeck OpponentDeck,
    string ChallengerPolicy,
    string OpponentPolicy
);

public sealed record RiftboundLegalAction(
    string ActionId,
    RiftboundActionType ActionType,
    int PlayerIndex,
    string Description
);

public sealed record RiftboundSimulationEngineResult(
    bool Succeeded,
    string? ErrorMessage,
    GameSession Session,
    IReadOnlyCollection<RiftboundLegalAction> LegalActions
);
