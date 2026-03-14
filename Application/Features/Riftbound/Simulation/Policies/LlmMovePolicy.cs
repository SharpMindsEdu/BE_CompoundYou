using System.Text;
using Application.Features.Riftbound.Simulation.Engine;
using Domain.Services.Ai;
using Microsoft.Extensions.Logging;

namespace Application.Features.Riftbound.Simulation.Policies;

public sealed class LlmMovePolicy(
    IAiService aiService,
    HeuristicMovePolicy fallback,
    ILogger<LlmMovePolicy> logger
) : IMovePolicy
{
    public const string Id = "llm";

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

        var legalActionIds = legalActions.Select(a => a.ActionId).ToList();
        var prompt = BuildPrompt(context, legalActions);

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(8));

            var actionId = await aiService.SelectActionIdAsync(
                prompt,
                legalActionIds,
                timeoutCts.Token
            );
            if (
                !string.IsNullOrWhiteSpace(actionId)
                && legalActionIds.Contains(actionId, StringComparer.Ordinal)
            )
            {
                return actionId;
            }
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning(
                "LLM move selection timed out for simulation {SimulationId}. Falling back to heuristic.",
                context.Session.SimulationId
            );
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "LLM move selection failed for simulation {SimulationId}. Falling back to heuristic.",
                context.Session.SimulationId
            );
        }

        return await fallback.ChooseActionIdAsync(context, cancellationToken);
    }

    private static string BuildPrompt(
        RiftboundMovePolicyContext context,
        IReadOnlyCollection<RiftboundLegalAction> legalActions
    )
    {
        var me = context.Session.Players.Single(p => p.PlayerIndex == context.PlayerIndex);
        var opponent = context.Session.Players.Single(p => p.PlayerIndex != context.PlayerIndex);

        var builder = new StringBuilder();
        builder.AppendLine("Choose exactly one legal actionId.");
        builder.AppendLine($"Ruleset: {context.Session.RulesetVersion.Value}");
        builder.AppendLine(
            $"Turn: {context.Session.TurnNumber}, phase: {context.Session.Phase}, state: {context.Session.State}"
        );
        builder.AppendLine($"Your score: {me.Score}, opponent score: {opponent.Score}");
        builder.AppendLine($"Your hand: {me.HandZone.Cards.Count} cards");
        builder.AppendLine($"Your rune energy: {me.RunePool.Energy}");
        builder.AppendLine($"Your base units: {me.BaseZone.Cards.Count}");

        var controlledBattlefields = context
            .Session.Battlefields.Where(b => b.ControlledByPlayerIndex == context.PlayerIndex)
            .Select(b => b.Name)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
        builder.AppendLine(
            controlledBattlefields.Count == 0
                ? "You control no battlefields."
                : $"You control battlefields: {string.Join(", ", controlledBattlefields)}"
        );

        builder.AppendLine("Legal actions:");
        foreach (var action in legalActions.OrderBy(a => a.ActionId, StringComparer.Ordinal))
        {
            builder.AppendLine($"- {action.ActionId} :: {action.Description}");
        }

        builder.AppendLine("Return only one actionId from the list.");
        return builder.ToString();
    }
}
