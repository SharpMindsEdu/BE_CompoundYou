using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class GustEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "gust";
    public override string TemplateId => "named.gust";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var battlefield in session.Battlefields)
        {
            foreach (var target in battlefield.Units)
            {
                if (runtime.GetEffectiveMight(session, target) > 3)
                {
                    continue;
                }

                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"Play {card.Name} returning {target.Name}"
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
        var target = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        var battlefield = target is null
            ? null
            : RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId);
        if (target is null || battlefield is null || runtime.GetEffectiveMight(session, target) > 3)
        {
            return;
        }

        battlefield.Units.Remove(target);
        session.Players[target.OwnerPlayerIndex].HandZone.Cards.Add(target);
    }
}

