using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class ClockworkKeeperEffect : RiftboundNamedCardEffectBase
{
    public const string AdditionalCostMarker = "-clockwork-additional-cost-";

    public override string NameIdentifier => "clockwork-keeper";
    public override string TemplateId => "named.clockwork-keeper";

    public override bool TryAddUnitPlayLegalActions(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        List<RiftboundLegalAction> actions
    )
    {
        var destinations = new List<(string Suffix, string Description)>
        {
            ("-to-base", $"Play {card.Name} to base"),
        };
        destinations.AddRange(
            session.Battlefields
                .Where(x => x.ControlledByPlayerIndex == player.PlayerIndex)
                .Select(x => ($"-to-bf-{x.Index}", $"Play {card.Name} to battlefield {x.Name}"))
        );

        foreach (var destination in destinations)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}{destination.Suffix}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    destination.Description
                )
            );
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}{AdditionalCostMarker}{destination.Suffix}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"{destination.Description} (+[Calm], draw 1)"
                )
            );
        }

        return true;
    }

    public override void OnUnitPlay(
        IRiftboundEffectRuntime runtime,
        GameSession session,
        PlayerState player,
        CardInstance card,
        string actionId
    )
    {
        if (!actionId.Contains(AdditionalCostMarker, StringComparison.Ordinal))
        {
            return;
        }

        runtime.DrawCards(player, 1);
    }
}

