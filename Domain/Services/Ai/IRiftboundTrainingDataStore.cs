namespace Domain.Services.Ai;

public sealed record RiftboundActionTrainingSample(
    DateTimeOffset CreatedAt,
    string PolicyId,
    string SelectionSource,
    string? SelectedActionId,
    RiftboundActionDecisionRequest Decision
);

public sealed record RiftboundDeckBuildTrainingSample(
    DateTimeOffset CreatedAt,
    string SelectionSource,
    RiftboundDeckBuildRequest Request,
    RiftboundDeckBuildProposal Proposal
);

public interface IRiftboundTrainingDataStore
{
    Task RecordActionSampleAsync(
        RiftboundActionTrainingSample sample,
        CancellationToken cancellationToken = default
    );

    Task RecordDeckBuildSampleAsync(
        RiftboundDeckBuildTrainingSample sample,
        CancellationToken cancellationToken = default
    );
}
