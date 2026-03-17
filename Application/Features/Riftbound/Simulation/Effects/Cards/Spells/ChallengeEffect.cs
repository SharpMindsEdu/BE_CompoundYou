using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ChallengeEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "challenge";
    public override string TemplateId => "named.challenge";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var friendly = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex).ToList();
        var enemy = RiftboundEffectUnitTargeting.EnumerateAllUnits(session)
            .Where(x => x.ControllerPlayerIndex != player.PlayerIndex)
            .ToList();
        foreach (var source in friendly)
        {
            foreach (var target in enemy)
            {
                var ids = $"{source.InstanceId},{target.InstanceId}";
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell{runtime.MultiTargetUnitsMarker}{ids}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name}: {source.Name} challenges {target.Name}"
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
        var selected = RiftboundEffectUnitTargeting.ResolveTargetUnitsFromAction(session, actionId).ToList();
        var friendly = selected.FirstOrDefault(x => x.ControllerPlayerIndex == player.PlayerIndex);
        var enemy = selected.FirstOrDefault(x => x.ControllerPlayerIndex != player.PlayerIndex);
        if (friendly is null || enemy is null)
        {
            return;
        }

        var damageToFriendly = runtime.GetEffectiveMight(session, enemy);
        var damageToEnemy = runtime.GetEffectiveMight(session, friendly);
        friendly.MarkedDamage += damageToFriendly;
        enemy.MarkedDamage += damageToEnemy;
    }
}

