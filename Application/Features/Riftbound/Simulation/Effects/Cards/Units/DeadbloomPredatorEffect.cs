using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class DeadbloomPredatorEffect : RiftboundNamedCardEffectBase
{
    public override string NameIdentifier => "deadbloom-predator";
    public override string TemplateId => "named.deadbloom-predator";

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
                .OrderBy(x => x.Index)
                .Select(x => ($"-to-bf-{x.Index}", $"Play {card.Name} to battlefield {x.Name}"))
        );
        destinations.AddRange(
            session.Battlefields
                .Where(x =>
                    x.ControlledByPlayerIndex != player.PlayerIndex
                    && x.Units.Any(unit => unit.ControllerPlayerIndex != player.PlayerIndex))
                .OrderBy(x => x.Index)
                .Select(x => ($"-to-bf-{x.Index}", $"Play {card.Name} to occupied enemy battlefield {x.Name}"))
        );

        foreach (var destination in destinations.Distinct())
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

        return true;
    }
}
