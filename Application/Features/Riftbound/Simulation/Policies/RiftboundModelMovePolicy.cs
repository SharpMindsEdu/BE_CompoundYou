using Application.Features.Riftbound.Simulation.Engine;
using Domain.Services.Ai;
using Microsoft.Extensions.Logging;

namespace Application.Features.Riftbound.Simulation.Policies;

public sealed class RiftboundModelMovePolicy(
    IRiftboundAiModelService modelService,
    HeuristicMovePolicy fallback,
    ILogger<RiftboundModelMovePolicy> logger,
    IRiftboundTrainingDataStore? trainingDataStore = null
) : IMovePolicy
{
    public const string Id = "riftbound-model";

    public string PolicyId => Id;

    public async Task<string?> ChooseActionIdAsync(
        RiftboundMovePolicyContext context,
        CancellationToken cancellationToken
    )
    {
        var legalActions = context
            .LegalActions.Where(a => a.PlayerIndex == context.PlayerIndex)
            .OrderBy(a => a.ActionId, StringComparer.Ordinal)
            .ToList();
        if (legalActions.Count == 0)
        {
            return null;
        }

        var legalActionIds = legalActions.Select(a => a.ActionId).ToHashSet(StringComparer.Ordinal);
        var decision = RiftboundAiDecisionRequestBuilder.Build(context);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

            var rawActionId = decision.DecisionKind == RiftboundDecisionKind.ReactionSelection
                ? await modelService.SelectReactionIdAsync(decision, timeoutCts.Token)
                : await modelService.SelectActionIdAsync(decision, timeoutCts.Token);
            var selectedActionId = NormalizeActionId(rawActionId, legalActionIds);

            if (
                !string.IsNullOrWhiteSpace(selectedActionId)
                && legalActionIds.Contains(selectedActionId)
            )
            {
                await TryRecordActionSampleAsync(decision, selectedActionId, "model", cancellationToken);
                return selectedActionId;
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "Riftbound model move selection timed out for simulation {SimulationId}. Falling back to heuristic.",
                context.Session.SimulationId
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Riftbound model move selection failed for simulation {SimulationId}. Falling back to heuristic.",
                context.Session.SimulationId
            );
        }

        var fallbackActionId = await fallback.ChooseActionIdAsync(context, cancellationToken);
        await TryRecordActionSampleAsync(
            decision,
            fallbackActionId,
            "heuristic-fallback",
            cancellationToken
        );
        return fallbackActionId;
    }

    private static string? NormalizeActionId(string? rawActionId, IReadOnlyCollection<string> legalActionIds)
    {
        if (string.IsNullOrWhiteSpace(rawActionId))
        {
            return null;
        }

        var normalized = rawActionId.Trim().Trim('`', '"', '\'');
        if (legalActionIds.Contains(normalized, StringComparer.Ordinal))
        {
            return normalized;
        }

        return legalActionIds.FirstOrDefault(actionId =>
            normalized.Contains(actionId, StringComparison.Ordinal)
        );
    }

    private async Task TryRecordActionSampleAsync(
        RiftboundActionDecisionRequest decision,
        string? selectedActionId,
        string selectionSource,
        CancellationToken cancellationToken
    )
    {
        if (trainingDataStore is null || string.IsNullOrWhiteSpace(selectedActionId))
        {
            return;
        }

        try
        {
            await trainingDataStore.RecordActionSampleAsync(
                new RiftboundActionTrainingSample(
                    DateTimeOffset.UtcNow,
                    PolicyId,
                    selectionSource,
                    selectedActionId,
                    decision
                ),
                cancellationToken
            );
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to record action training sample for simulation {SimulationId}.",
                decision.SimulationId
            );
        }
    }
}
