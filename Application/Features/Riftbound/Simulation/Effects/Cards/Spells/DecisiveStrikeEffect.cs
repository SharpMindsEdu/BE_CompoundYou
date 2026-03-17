using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DecisiveStrikeEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "decisive-strike";
    public override string TemplateId => "named.decisive-strike";

    public override bool TryAddLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        actions.Add(
            new RiftboundLegalAction(
                $"{runtime.ActionPrefix}play-{card.InstanceId}-spell",
                RiftboundActionType.PlayCard,
                player.PlayerIndex,
                $"Play spell {card.Name}"
            )
        );
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
        foreach (var unit in RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex))
        {
            unit.TemporaryMightModifier += 2;
        }
    }
}

