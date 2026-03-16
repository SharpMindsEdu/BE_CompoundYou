namespace Domain.Services.Ai;

public enum RiftboundDecisionKind
{
    ActionSelection,
    ReactionSelection,
}

public sealed record RiftboundActionCandidate(
    string ActionId,
    string ActionType,
    string Description
);

public sealed record RiftboundActionDecisionRequest(
    long SimulationId,
    string RulesetVersion,
    int TurnNumber,
    string Phase,
    string State,
    int PlayerIndex,
    int OpponentIndex,
    int MyScore,
    int OpponentScore,
    int MyHandCount,
    int MyRuneEnergy,
    int MyBaseUnits,
    IReadOnlyCollection<string> ControlledBattlefields,
    IReadOnlyCollection<RiftboundActionCandidate> LegalActions,
    string? LastOpponentActionId,
    RiftboundDecisionKind DecisionKind
);

public sealed record RiftboundDeckBuildPool(
    long LegendId,
    IReadOnlyCollection<long> ChampionIds,
    IReadOnlyCollection<long> MainDeckCardIds,
    IReadOnlyCollection<long> RuneCardIds,
    IReadOnlyCollection<long> BattlefieldCardIds,
    IReadOnlyCollection<string> Colors
);

public sealed record RiftboundDeckBuildRequest(
    long RunId,
    int Generation,
    long RequestedByUserId,
    long Seed,
    int MainDeckCardCount,
    int SideboardCardCount,
    int RuneDeckCardCount,
    int BattlefieldCardCount,
    RiftboundDeckBuildPool Pool
);

public sealed record RiftboundDeckCardSelection(long CardId, int Quantity);

public sealed record RiftboundDeckBuildProposal(
    long LegendId,
    long ChampionId,
    IReadOnlyCollection<RiftboundDeckCardSelection> MainDeck,
    IReadOnlyCollection<RiftboundDeckCardSelection> Sideboard,
    IReadOnlyCollection<RiftboundDeckCardSelection> RuneDeck,
    IReadOnlyCollection<long> BattlefieldIds
);

public interface IRiftboundAiModelService
{
    Task<string?> SelectActionIdAsync(
        RiftboundActionDecisionRequest request,
        CancellationToken cancellationToken = default
    );

    Task<string?> SelectReactionIdAsync(
        RiftboundActionDecisionRequest request,
        CancellationToken cancellationToken = default
    );

    Task<RiftboundDeckBuildProposal?> BuildDeckAsync(
        RiftboundDeckBuildRequest request,
        CancellationToken cancellationToken = default
    );
}
