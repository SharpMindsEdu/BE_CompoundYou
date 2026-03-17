using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DetonateEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "detonate";
    public override string TemplateId => "named.detonate";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var gear in RiftboundEffectGearTargeting.EnumerateAllGear(session))
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-gear-{gear.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {gear.Name}"
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
        var targetGear = RiftboundEffectGearTargeting.ResolveTargetGearFromAction(session, actionId);
        if (targetGear is null)
        {
            return;
        }

        if (!RiftboundEffectGearTargeting.RemoveGearFromBoard(session, targetGear))
        {
            return;
        }

        var owner = RiftboundEffectGearTargeting.ResolveOwnerPlayer(session, targetGear);
        targetGear.AttachedToInstanceId = null;
        owner.TrashZone.Cards.Add(targetGear);
        runtime.DrawCards(owner, 2);
    }
}

