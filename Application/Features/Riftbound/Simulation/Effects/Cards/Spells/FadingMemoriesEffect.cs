using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FadingMemoriesEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "fading-memories";
    public override string TemplateId => "named.fading-memories";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        foreach (var target in session.Battlefields.SelectMany(x => x.Units))
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
        var unitTarget = RiftboundEffectUnitTargeting.ResolveTargetUnitFromAction(session, actionId);
        if (unitTarget is not null && session.Battlefields.Any(x => x.Units.Any(y => y.InstanceId == unitTarget.InstanceId)))
        {
            unitTarget.EffectData["temporaryUntilBeginning"] = "true";
            return;
        }

        var gearTarget = RiftboundEffectGearTargeting.ResolveTargetGearFromAction(session, actionId);
        if (gearTarget is not null)
        {
            gearTarget.EffectData["temporaryUntilBeginning"] = "true";
        }
    }
}

