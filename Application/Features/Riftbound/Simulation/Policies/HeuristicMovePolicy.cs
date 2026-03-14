using Application.Features.Riftbound.Simulation.Engine;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Policies;

public sealed class HeuristicMovePolicy : IMovePolicy
{
    public const string Id = "heuristic";

    public string PolicyId => Id;

    public Task<string?> ChooseActionIdAsync(
        RiftboundMovePolicyContext context,
        CancellationToken cancellationToken
    )
    {
        var actionId = PickAction(context);
        return Task.FromResult(actionId);
    }

    internal static string? PickAction(RiftboundMovePolicyContext context)
    {
        var candidates = context
            .LegalActions.Where(a => a.PlayerIndex == context.PlayerIndex)
            .OrderByDescending(GetPriority)
            .ThenBy(a => a.ActionId, StringComparer.Ordinal)
            .ToList();

        return candidates.FirstOrDefault()?.ActionId;
    }

    private static int GetPriority(RiftboundLegalAction action)
    {
        return action.ActionType switch
        {
            RiftboundActionType.PlayCard when action.ActionId.Contains("-to-bf-", StringComparison.Ordinal) => 90,
            RiftboundActionType.PlayCard => 80,
            RiftboundActionType.ActivateRune => 70,
            RiftboundActionType.StandardMove when action.ActionId.Contains("-to-bf-", StringComparison.Ordinal) => 60,
            RiftboundActionType.StandardMove => 50,
            RiftboundActionType.ResolveCombat => 40,
            RiftboundActionType.PassFocus => 30,
            RiftboundActionType.EndTurn => 0,
            _ => 10,
        };
    }
}
