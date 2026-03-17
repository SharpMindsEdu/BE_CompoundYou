using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BrazenBuccaneerEffect : RiftboundNamedCardEffectBase
{
    public const string DiscardMarker = "-brazen-discard-";

    public override string NameIdentifier => "brazen-buccaneer";
    public override string TemplateId => "named.brazen-buccaneer";

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
        }

        var discardCandidates = player.HandZone.Cards.Where(x => x.InstanceId != card.InstanceId).ToList();
        foreach (var discard in discardCandidates)
        {
            foreach (var destination in destinations)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}{DiscardMarker}{discard.InstanceId}{destination.Suffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"{destination.Description} (discard {discard.Name}, cost -2)"
                    )
                );
            }
        }

        return true;
    }
}
