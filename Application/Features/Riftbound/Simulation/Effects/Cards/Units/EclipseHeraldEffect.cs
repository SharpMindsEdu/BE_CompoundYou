using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EclipseHeraldEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "eclipse-herald";
    public override string TemplateId => "named.eclipse-herald";

    public override void OnFriendlyCardPlayed(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        CardInstance playedCard,
        string actionId
    )
    {
        if (!string.Equals(playedCard.EffectTemplateId, "named.facebreaker", StringComparison.Ordinal))
        {
            return;
        }

        var markerIndex = actionId.IndexOf(FacebreakerEffect.EnemyMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return;
        }

        var fragment = actionId[(markerIndex + FacebreakerEffect.EnemyMarker.Length)..];
        var enemyIdText = fragment.Length >= 36 ? fragment[..36] : string.Empty;
        if (!Guid.TryParse(enemyIdText, out var enemyId))
        {
            return;
        }

        var enemy = RiftboundEffectUnitTargeting.EnumerateAllUnits(session).FirstOrDefault(x => x.InstanceId == enemyId);
        if (enemy is null || enemy.ControllerPlayerIndex == player.PlayerIndex)
        {
            return;
        }

        card.IsExhausted = false;
        card.TemporaryMightModifier += 1;
    }
}

