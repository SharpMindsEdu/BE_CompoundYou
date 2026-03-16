using Application.Features.Riftbound.Simulation.Engine;
using Domain.Services.Ai;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Policies;

internal static class RiftboundAiDecisionRequestBuilder
{
    internal static RiftboundActionDecisionRequest Build(RiftboundMovePolicyContext context)
    {
        var legalActions = context
            .LegalActions.Where(a => a.PlayerIndex == context.PlayerIndex)
            .OrderBy(a => a.ActionId, StringComparer.Ordinal)
            .ToArray();
        if (legalActions.Length == 0)
        {
            return BuildEmptyDecision(context);
        }

        var me = context.Session.Players.Single(p => p.PlayerIndex == context.PlayerIndex);
        var opponent = context.Session.Players.Single(p => p.PlayerIndex != context.PlayerIndex);

        var controlledBattlefields = context
            .Session.Battlefields.Where(b => b.ControlledByPlayerIndex == context.PlayerIndex)
            .Select(b => b.Name)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var lastOpponentActionId = context
            .Session.Chain.Where(x => x.ControllerPlayerIndex != context.PlayerIndex)
            .Select(x => x.ActionId)
            .LastOrDefault();

        var kind = legalActions.Any(a =>
            a.ActionType is RiftboundActionType.ResolveCombat or RiftboundActionType.PassFocus
        )
            ? RiftboundDecisionKind.ReactionSelection
            : RiftboundDecisionKind.ActionSelection;

        return new RiftboundActionDecisionRequest(
            context.Session.SimulationId,
            context.Session.RulesetVersion.Value,
            context.Session.TurnNumber,
            context.Session.Phase.ToString(),
            context.Session.State.ToString(),
            context.PlayerIndex,
            opponent.PlayerIndex,
            me.Score,
            opponent.Score,
            me.HandZone.Cards.Count,
            me.RunePool.Energy,
            me.BaseZone.Cards.Count,
            controlledBattlefields,
            legalActions
                .Select(a => new RiftboundActionCandidate(a.ActionId, a.ActionType.ToString(), a.Description))
                .ToArray(),
            lastOpponentActionId,
            kind
        );
    }

    private static RiftboundActionDecisionRequest BuildEmptyDecision(RiftboundMovePolicyContext context)
    {
        return new RiftboundActionDecisionRequest(
            context.Session.SimulationId,
            context.Session.RulesetVersion.Value,
            context.Session.TurnNumber,
            context.Session.Phase.ToString(),
            context.Session.State.ToString(),
            context.PlayerIndex,
            context.PlayerIndex == 0 ? 1 : 0,
            0,
            0,
            0,
            0,
            0,
            [],
            [],
            null,
            RiftboundDecisionKind.ActionSelection
        );
    }
}
