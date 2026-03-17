using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class BardMercurialEffect : RiftboundNamedCardEffectBase
{
    private const string AdditionalCostActionMarker = "-bard-exhaust-legend-";

    public override string NameIdentifier => "bard-mercurial";
    public override string TemplateId => "named.bard-mercurial";

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

        if (!player.LegendZone.Cards.Any(x => !x.IsExhausted))
        {
            return true;
        }

        foreach (var destination in destinations)
        {
            actions.Add(
                new RiftboundLegalAction(
                    $"{runtime.ActionPrefix}play-{card.InstanceId}{AdditionalCostActionMarker}{destination.Suffix}",
                    RiftboundActionType.PlayCard,
                    player.PlayerIndex,
                    $"{destination.Description} (+ exhaust your legend)"
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
        if (!actionId.Contains(AdditionalCostActionMarker, StringComparison.Ordinal))
        {
            return;
        }

        var legend = player.LegendZone.Cards.FirstOrDefault(x => !x.IsExhausted);
        if (legend is null)
        {
            return;
        }

        legend.IsExhausted = true;

        var destination = session.Battlefields
            .Where(x =>
                x.ControlledByPlayerIndex is null
                && x.ContestedByPlayerIndex is null
                && x.Units.Count == 0
            )
            .OrderBy(x => x.Index)
            .FirstOrDefault();
        if (destination is null)
        {
            runtime.AddEffectContext(
                session,
                card.Name,
                player.PlayerIndex,
                "WhenPlay",
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["template"] = card.EffectTemplateId,
                    ["paidAdditionalCost"] = "true",
                    ["movedUnits"] = "0",
                }
            );
            return;
        }

        var movableUnits = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.InstanceId != card.InstanceId)
            .ToList();

        foreach (var unit in movableUnits)
        {
            RemoveUnitFromCurrentLocation(session, player, unit);
            destination.Units.Add(unit);
        }

        if (destination.ControlledByPlayerIndex != player.PlayerIndex)
        {
            destination.ContestedByPlayerIndex = player.PlayerIndex;
        }

        runtime.AddEffectContext(
            session,
            card.Name,
            player.PlayerIndex,
            "WhenPlay",
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["template"] = card.EffectTemplateId,
                ["paidAdditionalCost"] = "true",
                ["movedUnits"] = movableUnits.Count.ToString(),
                ["battlefield"] = destination.Name,
            }
        );
    }

    private static void RemoveUnitFromCurrentLocation(
        GameSession session,
        PlayerState player,
        CardInstance unit
    )
    {
        if (player.BaseZone.Cards.Remove(unit))
        {
            return;
        }

        foreach (var battlefield in session.Battlefields)
        {
            if (battlefield.Units.Remove(unit))
            {
                return;
            }
        }
    }
}

