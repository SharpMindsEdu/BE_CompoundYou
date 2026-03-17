using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DisintegrateEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "disintegrate";
    public override string TemplateId => "named.disintegrate";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var unit in session.Battlefields.SelectMany(x => x.Units))
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{unit.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {unit.Name}"
                )
            );
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
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (
            target is null
            || RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId) is null
        )
        {
            return;
        }

        var magnitude = 3 + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        var targetMight = runtime.GetEffectiveMight(session, target);
        target.MarkedDamage += magnitude;
        if (magnitude >= targetMight && targetMight > 0)
        {
            runtime.DrawCards(player, 1);
        }
    }
}

