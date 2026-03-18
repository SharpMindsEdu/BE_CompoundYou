using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class FallingCometEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "falling-comet";
    public override string TemplateId => "named.falling-comet";

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
        if (target is null)
        {
            return;
        }

        if (RiftboundEffectUnitTargeting.FindBattlefieldContainingUnit(session, target.InstanceId) is null)
        {
            return;
        }

        var magnitude = runtime.ReadMagnitude(card, fallback: 6)
            + runtime.GetSpellAndAbilityBonusDamage(session, player.PlayerIndex);
        target.MarkedDamage += magnitude;
    }
}
