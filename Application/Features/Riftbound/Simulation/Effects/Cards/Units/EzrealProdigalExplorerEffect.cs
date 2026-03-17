using System.Globalization;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EzrealProdigalExplorerEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "ezreal-prodigal-explorer";
    public override string TemplateId => "named.ezreal-prodigal-explorer";
    public override bool HasActivatedAbility => true;

    public override bool TryActivateAbility(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.IsExhausted || CountEnemySelectionsThisTurn(session, player.PlayerIndex) < 2)
        {
            return false;
        }

        card.IsExhausted = true;
        runtime.DrawCards(player, 1);
        return true;
    }

    private static int CountEnemySelectionsThisTurn(GameSession session, int playerIndex)
    {
        return session.EffectContexts.Count(context =>
            context.ControllerPlayerIndex == playerIndex
            && string.Equals(context.Timing, "Play", StringComparison.OrdinalIgnoreCase)
            && context.Metadata.TryGetValue("turn", out var turnText)
            && int.TryParse(turnText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var turn)
            && turn == session.TurnNumber
            && context.Metadata.TryGetValue("actionId", out var actionId)
            && IsEnemyTargetAction(session, playerIndex, actionId)
        );
    }

    private static bool IsEnemyTargetAction(GameSession session, int playerIndex, string actionId)
    {
        return actionId.Contains("-target-unit-", StringComparison.OrdinalIgnoreCase)
            || actionId.Contains("-target-gear-", StringComparison.OrdinalIgnoreCase);
    }
}
