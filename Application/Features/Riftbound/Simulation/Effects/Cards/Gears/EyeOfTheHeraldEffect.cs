using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class EyeOfTheHeraldEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "eye-of-the-herald";
    public override string TemplateId => "named.eye-of-the-herald";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var target in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex))
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}-spell-target-unit-{target.InstanceId}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"Play {card.Name} targeting {target.Name}"
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
        if (target is null || target.ControllerPlayerIndex != player.PlayerIndex)
        {
            return;
        }

        card.AttachedToInstanceId = target.InstanceId;
        card.IsExhausted = true;
        var battlefield = RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId);
        if (battlefield is not null)
        {
            battlefield.Gear.Add(card);
        }
        else
        {
            player.BaseZone.Cards.Add(card);
        }

        runtime.NotifyGearAttached(session, card, target);
    }

    public override void OnUnitMove(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card
    )
    {
        if (card.AttachedToInstanceId is null)
        {
            return;
        }

        var battlefield = session.Battlefields.FirstOrDefault(x =>
            x.Units.Any(unit => unit.InstanceId == card.AttachedToInstanceId.Value)
        );
        if (battlefield is null)
        {
            return;
        }

        battlefield.Units.Add(
            RiftboundTokenFactory.CreateRecruitUnitToken(
                ownerPlayerIndex: player.PlayerIndex,
                controllerPlayerIndex: player.PlayerIndex,
                might: 1,
                exhausted: true
            )
        );
    }
}

