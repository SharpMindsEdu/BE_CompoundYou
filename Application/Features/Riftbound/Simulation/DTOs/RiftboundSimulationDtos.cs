using Application.Features.Riftbound.Simulation.Engine;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.DTOs;

public sealed record RiftboundDeckSimulationSupportDto(
    long DeckId,
    string RulesetVersion,
    IReadOnlyCollection<string> RuleCorrections,
    bool IsSimulationReady,
    IReadOnlyCollection<string> ValidationIssues,
    IReadOnlyCollection<string> UnsupportedCards
);

public sealed record RiftboundSimulationPlayerScoreDto(
    int PlayerIndex,
    long DeckId,
    string Policy,
    int Score
);

public sealed record RiftboundSimulationBattlefieldDto(
    int Index,
    long CardId,
    string Name,
    int? ControlledByPlayerIndex,
    int? ContestedByPlayerIndex,
    int Player0UnitCount,
    int Player1UnitCount
);

public sealed record RiftboundSimulationEventDto(
    int Sequence,
    string EventType,
    string PayloadJson,
    DateTimeOffset CreatedOn
);

public sealed record RiftboundSimulationDto(
    long SimulationId,
    long Seed,
    string RulesetVersion,
    string Mode,
    string Status,
    int TurnNumber,
    int TurnPlayerIndex,
    RiftboundTurnPhase Phase,
    RiftboundTurnState State,
    int? WinnerPlayerIndex,
    IReadOnlyCollection<RiftboundSimulationPlayerScoreDto> Scores,
    IReadOnlyCollection<RiftboundSimulationBattlefieldDto> Battlefields,
    IReadOnlyCollection<RiftboundLegalAction> LegalActions,
    IReadOnlyCollection<RiftboundSimulationEventDto> Events
);

public sealed record RiftboundDeckTestRunDto(
    long SimulationId,
    long Seed,
    string Status,
    int? WinnerPlayerIndex,
    IReadOnlyCollection<RiftboundSimulationPlayerScoreDto> Scores
);

public sealed record RiftboundDeckTestsResultDto(
    long ChallengerDeckId,
    long OpponentDeckId,
    string RulesetVersion,
    string ChallengerPolicy,
    string OpponentPolicy,
    int TotalRuns,
    int ChallengerWins,
    int OpponentWins,
    int Draws,
    IReadOnlyCollection<RiftboundDeckTestRunDto> Runs
);
