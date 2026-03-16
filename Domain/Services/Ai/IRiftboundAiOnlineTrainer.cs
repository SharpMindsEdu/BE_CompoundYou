namespace Domain.Services.Ai;

public sealed record RiftboundAiDecisionEvent(
    RiftboundActionDecisionRequest Decision,
    string SelectedActionId,
    int PlayerIndex
);

public sealed record RiftboundAiEpisode(
    string Source,
    long? SimulationId,
    int? WinnerPlayerIndex,
    IReadOnlyCollection<RiftboundAiDecisionEvent> Decisions
);

public sealed record RiftboundDeckTrainingCard(long CardId, int Quantity);

public sealed record RiftboundDeckTrainingOutcome(
    string Source,
    long? RunId,
    long DeckId,
    long LegendId,
    long ChampionId,
    IReadOnlyCollection<RiftboundDeckTrainingCard> MainDeck,
    IReadOnlyCollection<RiftboundDeckTrainingCard> Sideboard,
    IReadOnlyCollection<RiftboundDeckTrainingCard> RuneDeck,
    IReadOnlyCollection<long> BattlefieldIds,
    bool IsWinner,
    bool IsDraw
);

public interface IRiftboundAiOnlineTrainer
{
    Task TrainFromEpisodeAsync(
        RiftboundAiEpisode episode,
        CancellationToken cancellationToken = default
    );

    Task TrainDeckOutcomeAsync(
        RiftboundDeckTrainingOutcome outcome,
        CancellationToken cancellationToken = default
    );
}
