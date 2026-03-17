using Application.Features.Riftbound.Simulation.Engine;
using Domain.Entities.Riftbound;
using Domain.Simulation;

namespace Application.Features.Riftbound.Simulation.Effects;

public sealed class CommanderLedrosEffect : RiftboundNamedCardEffectBase
{
    public const string SacrificeListMarker = "-commander-ledros-sac-";

    public override string NameIdentifier => "commander-ledros";
    public override string TemplateId => "named.commander-ledros";

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

        var sacrifices = RiftboundEffectUnitTargeting.EnumerateFriendlyUnits(session, player.PlayerIndex)
            .Where(x => x.InstanceId != card.InstanceId)
            .ToList();
        foreach (var selection in EnumerateSacrificeSelections(sacrifices))
        {
            var ids = string.Join(",", selection.Select(x => x.InstanceId));
            foreach (var destination in destinations)
            {
                actions.Add(
                    new RiftboundLegalAction(
                        $"{runtime.ActionPrefix}play-{card.InstanceId}{SacrificeListMarker}{ids}{destination.Suffix}",
                        RiftboundActionType.PlayCard,
                        player.PlayerIndex,
                        $"{destination.Description} (kill {selection.Count} friendly unit(s), cost -{selection.Count} [Order])"
                    )
                );
            }
        }

        return true;
    }

    private static IReadOnlyCollection<IReadOnlyCollection<CardInstance>> EnumerateSacrificeSelections(
        IReadOnlyList<CardInstance> candidates
    )
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        if (candidates.Count > 6)
        {
            // Keep action count bounded: provide single-target and full-target sacrifice lines.
            var singles = candidates.Select(x => (IReadOnlyCollection<CardInstance>)[x]).ToList();
            singles.Add(candidates.ToList());
            return singles;
        }

        var results = new List<IReadOnlyCollection<CardInstance>>();
        var totalMasks = 1 << candidates.Count;
        for (var mask = 1; mask < totalMasks; mask += 1)
        {
            var picked = new List<CardInstance>();
            for (var index = 0; index < candidates.Count; index += 1)
            {
                if ((mask & (1 << index)) != 0)
                {
                    picked.Add(candidates[index]);
                }
            }

            if (picked.Count > 0)
            {
                results.Add(picked);
            }
        }

        return results;
    }
}
