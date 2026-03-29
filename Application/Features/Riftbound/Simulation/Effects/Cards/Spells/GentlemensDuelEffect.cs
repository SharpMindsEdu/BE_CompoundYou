using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GentlemensDuelEffect : RiftboundNamedCardEffectBase
{
    private const string FriendlyTargetMarker = "-gentlemens-duel-friendly-";

    public override string NameIdentifier => "gentlemen-s-duel";
    public override string TemplateId => "named.gentlemen-s-duel";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var friendlyUnits = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex).ToList();
        var enemyUnits = RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .ToList();
        foreach (var friendly in friendlyUnits)
        {
            foreach (var enemy in enemyUnits)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{FriendlyTargetMarker}{friendly.InstanceId}-target-unit-{enemy.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name}: +3 {friendly.Name}, then duel {enemy.Name}"
                    )
                );
            }
        }

        return true;
    }

    public override void OnSpellOrGearPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        var friendly = ResolveFriendlyTarget(session, actionId);
        var enemy = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            friendly is null
            || enemy is null
            || friendly.ControllerPlayerIndex != player.PlayerIndex
            || enemy.ControllerPlayerIndex == player.PlayerIndex
        )
        {
            return;
        }

        friendly.TemporaryMightModifier += 3;
        var damageToFriendly = runtime.GetEffectiveMight(session, enemy);
        var damageToEnemy = runtime.GetEffectiveMight(session, friendly);
        friendly.MarkedDamage += damageToFriendly;
        enemy.MarkedDamage += damageToEnemy;
    }

    private static CardInstance? ResolveFriendlyTarget(GameSession session, string actionId)
    {
        var markerIndex = actionId.IndexOf(FriendlyTargetMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return null;
        }

        var fragment = actionId[(markerIndex + FriendlyTargetMarker.Length)..];
        var guidText = fragment.Length >= 36 ? fragment[..36] : string.Empty;
        if (!Guid.TryParse(guidText, out var targetId))
        {
            return null;
        }

        return RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .FirstOrDefault(x => x.InstanceId == targetId);
    }
}

